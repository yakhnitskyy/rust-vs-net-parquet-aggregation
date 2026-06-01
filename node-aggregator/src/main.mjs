#!/usr/bin/env node

import { existsSync, statSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";
import { performance } from "node:perf_hooks";
import { DuckDBInstance } from "@duckdb/node-api";

const DEFAULT_FILE_NAME = "orders.parquet";
const DEFAULT_DATA_DIRECTORY = "data";
const REGION_NAMES = ["North", "South", "East", "West", "Central", "Online"];

const COUNT_FORMATTER = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });
const CURRENCY_FORMATTER = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2
});

async function main() {
  try {
    const options = parseOptions(process.argv.slice(2));
    const fullPath = path.resolve(options.path);

    if (!existsSync(fullPath)) {
      console.error(`Parquet file not found: ${fullPath}`);
      process.exitCode = 1;
      return;
    }

    const fileSize = statSync(fullPath).size;
    const threads = Math.max(1, os.availableParallelism?.() ?? os.cpus().length);

    console.log(`Reading ${fullPath}`);
    console.log(`File size: ${formatBytes(fileSize)}`);
    console.log(`DuckDB threads: ${formatCount(threads)}`);

    const startedAt = performance.now();
    const instance = await DuckDBInstance.create(":memory:", {
      threads: String(threads)
    });

    const connection = await instance.connect();

    await connection.run("SET preserve_insertion_order = false");
    await connection.run(`SET threads = ${threads}`);
    try {
      await connection.run("SET memory_limit = '80%'");
    } catch {
      // Some DuckDB builds do not accept this setting name.
    }

    const metadataSql = `
      WITH grouped AS (
        SELECT
          row_group_id,
          MAX(row_group_num_rows) AS row_group_rows
        FROM parquet_metadata(${sqlString(fullPath)})
        GROUP BY row_group_id
      )
      SELECT
        COALESCE(COUNT(*), 0) AS row_group_count,
        COALESCE(SUM(row_group_rows), 0) AS total_rows
      FROM grouped`;
    const metadataReader = await connection.runAndReadAll(metadataSql);
    const [metadata] = metadataReader.getRowObjectsJS();

    const rowGroupCount = Number(metadata?.row_group_count ?? 0);
    const expectedRows = Number(metadata?.total_rows ?? 0);

    if (rowGroupCount > 0) {
      console.log(
        `Row groups: ${formatCount(rowGroupCount)} (metadata), expected rows: ${formatCount(expectedRows)}`
      );
    }

    const aggregateSql = `
      WITH source AS (
        SELECT
          CAST(RegionId AS INTEGER) % ${REGION_NAMES.length} AS region_index,
          CAST(Quantity AS BIGINT) AS quantity,
          CAST(UnitPrice AS DOUBLE) AS unit_price
        FROM read_parquet(${sqlString(fullPath)})
      )
      SELECT
        region_index,
        COUNT(*)::BIGINT AS orders,
        COALESCE(SUM(quantity * unit_price), 0)::DOUBLE AS revenue
      FROM source
      GROUP BY region_index
      ORDER BY region_index`;

    const aggregateReader = await connection.runAndReadAll(aggregateSql);
    const aggregateRows = aggregateReader.getRowObjectsJS();

    const ordersByRegion = new Array(REGION_NAMES.length).fill(0);
    const revenueByRegion = new Array(REGION_NAMES.length).fill(0);

    for (const row of aggregateRows) {
      const index = Number(row.region_index);
      if (index >= 0 && index < REGION_NAMES.length) {
        ordersByRegion[index] = Number(row.orders);
        revenueByRegion[index] = Number(row.revenue);
      }
    }

    let rowsRead = 0;
    let totalRevenue = 0;
    for (let i = 0; i < REGION_NAMES.length; i += 1) {
      rowsRead += ordersByRegion[i];
      totalRevenue += revenueByRegion[i];
    }

    const elapsedMs = performance.now() - startedAt;
    const elapsedSeconds = Math.max(elapsedMs / 1000, 0.001);

    console.log();
    console.log("Aggregation by region");
    console.log("Region       Orders            Revenue");
    console.log("----------------------------------------------");

    for (let i = 0; i < REGION_NAMES.length; i += 1) {
      const region = REGION_NAMES[i].padEnd(10, " ");
      const orders = formatCount(ordersByRegion[i]).padStart(14, " ");
      const revenue = formatCurrency(revenueByRegion[i]).padStart(18, " ");
      console.log(`${region} ${orders} ${revenue}`);
    }

    console.log("----------------------------------------------");
    console.log(
      `${"Total".padEnd(10, " ")} ${formatCount(rowsRead).padStart(14, " ")} ${formatCurrency(totalRevenue).padStart(18, " ")}`
    );
    console.log();
    console.log(`Processed ${formatCount(rowsRead)} rows in ${formatElapsed(elapsedMs)}`);
    console.log(`Throughput: ${formatCount(Math.round(rowsRead / elapsedSeconds))} rows/sec`);
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}

function parseOptions(args) {
  if (args.length === 0) {
    return { path: defaultOrdersPath() };
  }

  const first = args[0];
  if (first === "-h" || first === "--help" || first === "help") {
    printUsage();
    process.exit(0);
  }

  if (first !== "--path") {
    throw new Error(`Unknown argument: ${first}`);
  }

  if (args.length < 2) {
    throw new Error("Missing value for --path");
  }

  if (args.length > 2) {
    throw new Error("Unexpected extra arguments");
  }

  return { path: args[1] };
}

function defaultOrdersPath() {
  return path.join(findRepositoryRoot(), DEFAULT_DATA_DIRECTORY, DEFAULT_FILE_NAME);
}

function findRepositoryRoot() {
  const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
  const starts = [process.cwd(), scriptDirectory];

  for (const start of starts) {
    let current = path.resolve(start);
    while (true) {
      if (
        existsSync(path.join(current, "dotnet-app")) &&
        existsSync(path.join(current, "rust-aggregator")) &&
        existsSync(path.join(current, "cpp-aggregator"))
      ) {
        return current;
      }

      const parent = path.dirname(current);
      if (parent === current) {
        break;
      }

      current = parent;
    }
  }

  return process.cwd();
}

function printUsage() {
  console.log(`Node.js Parquet Aggregator

Usage:
  node ./src/main.mjs
  node ./src/main.mjs --path C:\\path\\to\\orders.parquet

When --path is omitted, the app reads .\\data\\orders.parquet from the repository root.`);
}

function sqlString(value) {
  return `'${value.replaceAll("'", "''")}'`;
}

function formatCount(value) {
  return COUNT_FORMATTER.format(value);
}

function formatCurrency(value) {
  return CURRENCY_FORMATTER.format(value);
}

function formatBytes(bytes) {
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unit = 0;

  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }

  return `${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${units[unit]}`;
}

function formatElapsed(elapsedMs) {
  const total100Ns = Math.round(elapsedMs * 10_000);
  const totalSeconds = Math.floor(total100Ns / 10_000_000);

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const fraction = total100Ns % 10_000_000;

  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}.${String(fraction).padStart(7, "0")}`;
}

await main();
