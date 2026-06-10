using System.Data.Common;
using System.Diagnostics;
using DuckDB.NET.Data;

sealed class DuckDbAggregator
{
    private const string InMemoryTableName = "orders_memory";

    public AggregationResult Aggregate(string fullPath, int regionCount, AggregationSource source)
    {
        int threadCount = Math.Max(1, Environment.ProcessorCount);
        long[] ordersByRegion = new long[regionCount];
        double[] revenueByRegion = new double[regionCount];
        long rowGroupCount = 0;
        long expectedRows = 0;
        TimeSpan? loadElapsed = null;

        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        ExecuteNonQuery(connection, "SET preserve_insertion_order = false");
        ExecuteNonQuery(connection, $"SET threads = {threadCount}");
        TryExecuteNonQuery(connection, "SET memory_limit = '80%'");

        string metadataSql = $$"""
            WITH grouped AS (
                SELECT row_group_id, MAX(row_group_num_rows) AS row_group_rows
                FROM parquet_metadata('{{EscapeSqlString(fullPath)}}')
                GROUP BY row_group_id
            )
            SELECT
                COALESCE(COUNT(*), 0) AS row_group_count,
                COALESCE(SUM(row_group_rows), 0) AS total_rows
            FROM grouped
            """;

        using (DbCommand metadataCommand = connection.CreateCommand())
        {
            metadataCommand.CommandText = metadataSql;
            using DbDataReader reader = metadataCommand.ExecuteReader();
            if (reader.Read())
            {
                rowGroupCount = reader.GetInt64(0);
                expectedRows = reader.GetInt64(1);
            }
        }

        if (source == AggregationSource.Memory)
        {
            string loadSql = $$"""
                CREATE TEMP TABLE {{InMemoryTableName}} AS
                SELECT RegionId, Quantity, UnitPrice
                FROM read_parquet('{{EscapeSqlString(fullPath)}}')
                """;

            var loadStopwatch = Stopwatch.StartNew();
            ExecuteNonQuery(connection, loadSql);
            loadStopwatch.Stop();
            loadElapsed = loadStopwatch.Elapsed;
        }

        string sourceSql = source switch
        {
            AggregationSource.File => $"read_parquet('{EscapeSqlString(fullPath)}')",
            AggregationSource.Memory => InMemoryTableName,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown aggregation source.")
        };

        string aggregateSql = $$"""
            WITH source AS (
                SELECT
                    CAST(RegionId AS INTEGER) % {{regionCount}} AS region_index,
                    CAST(Quantity AS BIGINT) AS quantity,
                    CAST(UnitPrice AS DOUBLE) AS unit_price
                FROM {{sourceSql}}
            )
            SELECT
                region_index,
                COUNT(*)::BIGINT AS orders,
                COALESCE(SUM(quantity * unit_price), 0)::DOUBLE AS revenue
            FROM source
            GROUP BY region_index
            ORDER BY region_index
            """;

        var stopwatch = Stopwatch.StartNew();
        using (DbCommand aggregateCommand = connection.CreateCommand())
        {
            aggregateCommand.CommandText = aggregateSql;
            using DbDataReader reader = aggregateCommand.ExecuteReader();
            while (reader.Read())
            {
                int regionIndex = reader.GetInt32(0);
                if ((uint)regionIndex >= (uint)regionCount)
                {
                    continue;
                }

                ordersByRegion[regionIndex] = reader.GetInt64(1);
                revenueByRegion[regionIndex] = reader.GetDouble(2);
            }
        }

        stopwatch.Stop();

        long rowsRead = 0;
        double totalRevenue = 0;
        for (int i = 0; i < regionCount; i++)
        {
            rowsRead += ordersByRegion[i];
            totalRevenue += revenueByRegion[i];
        }

        return new AggregationResult(
            FullPath: fullPath,
            Source: source,
            FileSize: new FileInfo(fullPath).Length,
            ThreadCount: threadCount,
            RowGroupCount: rowGroupCount,
            ExpectedRows: expectedRows,
            LoadElapsed: loadElapsed,
            OrdersByRegion: ordersByRegion,
            RevenueByRegion: revenueByRegion,
            RowsRead: rowsRead,
            TotalRevenue: totalRevenue,
            Elapsed: stopwatch.Elapsed);
    }

    private static void ExecuteNonQuery(DuckDBConnection connection, string sql)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void TryExecuteNonQuery(DuckDBConnection connection, string sql)
    {
        try
        {
            ExecuteNonQuery(connection, sql);
        }
        catch
        {
        }
    }

    private static string EscapeSqlString(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
