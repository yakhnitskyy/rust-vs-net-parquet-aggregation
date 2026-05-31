using System.Diagnostics;
using System.Buffers;
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
        int regionCount = AppDefaults.RegionNames.Length;
        object mergeLock = new();

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

            int[] quantities = ArrayPool<int>.Shared.Rent(rowCount);
            double[] unitPrices = ArrayPool<double>.Shared.Rent(rowCount);
            byte[] regionIds = ArrayPool<byte>.Shared.Rent(rowCount);

            try
            {
                await rowGroupReader.ReadAsync<int>(quantityField, quantities);
                await rowGroupReader.ReadAsync<double>(unitPriceField, unitPrices);
                await rowGroupReader.ReadAsync<byte>(regionIdField, regionIds);

                Parallel.For(
                    fromInclusive: 0,
                    toExclusive: rowCount,
                    localInit: () => new RegionTotals(regionCount),
                    body: (i, _, local) =>
                    {
                        int region = regionIds[i];
                        if ((uint)region >= (uint)regionCount)
                        {
                            region %= regionCount;
                        }

                        local.Orders[region]++;
                        local.Revenue[region] += quantities[i] * unitPrices[i];
                        return local;
                    },
                    localFinally: local =>
                    {
                        lock (mergeLock)
                        {
                            for (int i = 0; i < regionCount; i++)
                            {
                                ordersByRegion[i] += local.Orders[i];
                                revenueByRegion[i] += local.Revenue[i];
                            }
                        }
                    });
            }
            finally
            {
                ArrayPool<int>.Shared.Return(quantities, clearArray: false);
                ArrayPool<double>.Shared.Return(unitPrices, clearArray: false);
                ArrayPool<byte>.Shared.Return(regionIds, clearArray: false);
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

    private sealed class RegionTotals
    {
        public RegionTotals(int regionCount)
        {
            Orders = new long[regionCount];
            Revenue = new double[regionCount];
        }

        public long[] Orders { get; }

        public double[] Revenue { get; }
    }
}
