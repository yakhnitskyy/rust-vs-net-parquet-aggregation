using System.Diagnostics;
using Parquet;
using Parquet.Schema;

sealed class OrderAggregator
{
    public async Task<int> AggregateAsync(CliOptions options)
    {
        string path = Path.GetFullPath(options.Path);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Parquet file not found: {path}");
            return 1;
        }

        double[] revenueByRegion = new double[AppDefaults.RegionNames.Length];
        long[] ordersByRegion = new long[AppDefaults.RegionNames.Length];
        long rowsRead = 0;

        Console.WriteLine($"Reading {path}");
        Console.WriteLine($"File size: {DisplayFormatting.FormatBytes(new FileInfo(path).Length)}");

        var stopwatch = Stopwatch.StartNew();
        await using ParquetReader reader = await ParquetReader.CreateAsync(path);

        DataField quantityField = reader.Schema.FindDataField("Quantity");
        DataField unitPriceField = reader.Schema.FindDataField("UnitPrice");
        DataField regionIdField = reader.Schema.FindDataField("RegionId");

        for (int groupIndex = 0; groupIndex < reader.RowGroupCount; groupIndex++)
        {
            using ParquetRowGroupReader rowGroupReader = reader.OpenRowGroupReader(groupIndex);
            int rowCount = checked((int)rowGroupReader.RowCount);

            int[] quantities = new int[rowCount];
            double[] unitPrices = new double[rowCount];
            byte[] regionIds = new byte[rowCount];

            await rowGroupReader.ReadAsync<int>(quantityField, quantities);
            await rowGroupReader.ReadAsync<double>(unitPriceField, unitPrices);
            await rowGroupReader.ReadAsync<byte>(regionIdField, regionIds);

            for (int i = 0; i < rowCount; i++)
            {
                int region = regionIds[i] % AppDefaults.RegionNames.Length;
                ordersByRegion[region]++;
                revenueByRegion[region] += quantities[i] * unitPrices[i];
            }

            rowsRead += rowCount;
            Console.WriteLine(
                $"Row group {groupIndex + 1:N0}/{reader.RowGroupCount:N0}: processed {rowsRead:N0} rows in {stopwatch.Elapsed}");
        }

        stopwatch.Stop();
        Console.WriteLine();
        Console.WriteLine("Aggregation by region");
        Console.WriteLine("Region       Orders            Revenue");
        Console.WriteLine("----------------------------------------------");

        long totalOrders = 0;
        double totalRevenue = 0;
        for (int i = 0; i < AppDefaults.RegionNames.Length; i++)
        {
            totalOrders += ordersByRegion[i];
            totalRevenue += revenueByRegion[i];
            Console.WriteLine(
                $"{AppDefaults.RegionNames[i],-10} {ordersByRegion[i],14:N0} {revenueByRegion[i],18:C2}");
        }

        Console.WriteLine("----------------------------------------------");
        Console.WriteLine($"{"Total",-10} {totalOrders,14:N0} {totalRevenue,18:C2}");
        Console.WriteLine();
        Console.WriteLine($"Processed {rowsRead:N0} rows in {stopwatch.Elapsed}");
        Console.WriteLine($"Throughput: {rowsRead / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001):N0} rows/sec");
        return 0;
    }
}
