import * as duckdb from "https://cdn.jsdelivr.net/npm/@duckdb/duckdb-wasm@1.29.0/+esm";

const REGION_NAMES = ["North", "South", "East", "West", "Central", "Online"];

const fileInput = document.getElementById("fileInput");
const runButton = document.getElementById("runButton");
const statusElement = document.getElementById("status");
const metaElement = document.getElementById("meta");
const resultsElement = document.getElementById("results");

let dbPromise;

runButton.addEventListener("click", async () => {
  const [file] = fileInput.files ?? [];
  if (!file) {
    setStatus("Pick a parquet file first.", "error");
    return;
  }

  runButton.disabled = true;
  metaElement.innerHTML = "";
  resultsElement.innerHTML = "";

  try {
    setStatus("Loading DuckDB-WASM...", "working");
    const db = await getDb();
    const conn = await db.connect();

    try {
      const startedAt = performance.now();
      const fileName = "upload.parquet";
      const fileBytes = new Uint8Array(await file.arrayBuffer());

      setStatus("Registering parquet buffer...", "working");
      await db.dropFile(fileName).catch(() => undefined);
      await db.registerFileBuffer(fileName, fileBytes);

      setStatus("Reading metadata and aggregating...", "working");

      const metadataResult = await conn.query(`
        WITH grouped AS (
          SELECT
            row_group_id,
            MAX(row_group_num_rows) AS row_group_rows
          FROM parquet_metadata('${fileName}')
          GROUP BY row_group_id
        )
        SELECT
          COALESCE(COUNT(*), 0) AS row_group_count,
          COALESCE(SUM(row_group_rows), 0) AS expected_rows
        FROM grouped
      `);

      const aggregateResult = await conn.query(`
        WITH source AS (
          SELECT
            CAST(RegionId AS INTEGER) % ${REGION_NAMES.length} AS region_index,
            CAST(Quantity AS BIGINT) AS quantity,
            CAST(UnitPrice AS DOUBLE) AS unit_price
          FROM read_parquet('${fileName}')
        )
        SELECT
          region_index,
          COUNT(*)::BIGINT AS orders,
          COALESCE(SUM(quantity * unit_price), 0)::DOUBLE AS revenue
        FROM source
        GROUP BY region_index
        ORDER BY region_index
      `);

      const elapsedMs = performance.now() - startedAt;
      const elapsedSeconds = Math.max(elapsedMs / 1000, 0.001);

      const metadata = metadataResult.toArray()[0] ?? {
        row_group_count: 0,
        expected_rows: 0
      };

      const ordersByRegion = new Array(REGION_NAMES.length).fill(0);
      const revenueByRegion = new Array(REGION_NAMES.length).fill(0);

      for (const row of aggregateResult.toArray()) {
        const index = Number(row.region_index);
        if (index >= 0 && index < REGION_NAMES.length) {
          ordersByRegion[index] = Number(row.orders);
          revenueByRegion[index] = Number(row.revenue);
        }
      }

      const rowsRead = ordersByRegion.reduce((sum, value) => sum + value, 0);
      const totalRevenue = revenueByRegion.reduce((sum, value) => sum + value, 0);
      const throughput = Math.round(rowsRead / elapsedSeconds);

      renderMeta({
        fileName: file.name,
        fileSize: formatBytes(file.size),
        rowGroups: Number(metadata.row_group_count),
        expectedRows: Number(metadata.expected_rows),
        elapsed: formatElapsed(elapsedMs),
        throughput
      });

      renderTable(ordersByRegion, revenueByRegion, rowsRead, totalRevenue);
      setStatus("Aggregation complete.", "success");
    } finally {
      await conn.close();
    }
  } catch (error) {
    setStatus(`Failed: ${error instanceof Error ? error.message : String(error)}`, "error");
  } finally {
    runButton.disabled = false;
  }
});

async function getDb() {
  if (!dbPromise) {
    dbPromise = createDb();
  }

  return dbPromise;
}

async function createDb() {
  const bundles = duckdb.getJsDelivrBundles();
  const bundle = await duckdb.selectBundle(bundles);
  const workerBlob = new Blob([`importScripts("${bundle.mainWorker}");`], { type: "text/javascript" });
  const workerUrl = URL.createObjectURL(workerBlob);
  const worker = new Worker(workerUrl);
  URL.revokeObjectURL(workerUrl);

  const logger = new duckdb.ConsoleLogger();
  const db = new duckdb.AsyncDuckDB(logger, worker);
  await db.instantiate(bundle.mainModule, bundle.pthreadWorker);
  return db;
}

function renderMeta(values) {
  const items = [
    ["File", values.fileName],
    ["File size", values.fileSize],
    ["Row groups", formatCount(values.rowGroups)],
    ["Expected rows", formatCount(values.expectedRows)],
    ["Elapsed", values.elapsed],
    ["Throughput", `${formatCount(values.throughput)} rows/sec`]
  ];

  metaElement.innerHTML = items
    .map(([k, v]) => `<div class="pill"><span class="k">${escapeHtml(k)}</span><span class="v">${escapeHtml(v)}</span></div>`)
    .join("");
}

function renderTable(ordersByRegion, revenueByRegion, rowsRead, totalRevenue) {
  const bodyRows = REGION_NAMES.map((region, index) => {
    return `<tr>
      <td>${escapeHtml(region)}</td>
      <td class="num">${formatCount(ordersByRegion[index])}</td>
      <td class="num">${formatCurrency(revenueByRegion[index])}</td>
    </tr>`;
  }).join("");

  resultsElement.innerHTML = `
    <table>
      <thead>
        <tr>
          <th>Region</th>
          <th>Orders</th>
          <th>Revenue</th>
        </tr>
      </thead>
      <tbody>
        ${bodyRows}
        <tr class="total">
          <td>Total</td>
          <td class="num">${formatCount(rowsRead)}</td>
          <td class="num">${formatCurrency(totalRevenue)}</td>
        </tr>
      </tbody>
    </table>
  `;
}

function setStatus(message, state) {
  statusElement.textContent = message;
  statusElement.className = state;
}

function formatCount(value) {
  return new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 }).format(value);
}

function formatCurrency(value) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);
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

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}
