using System.Diagnostics;
using Parquet;
using Parquet.Schema;

sealed class OrderDataGenerator
{
    public async Task<int> GenerateAsync(CliOptions options)
    {
        string path = Path.GetFullPath(options.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        var orderIdField = new DataField<long>("OrderId");
        var customerIdField = new DataField<int>("CustomerId");
        var productIdField = new DataField<int>("ProductId");
        var orderDateField = new DataField<DateTime>("OrderDateUtc");
        var quantityField = new DataField<int>("Quantity");
        var unitPriceField = new DataField<double>("UnitPrice");
        var regionIdField = new DataField<byte>("RegionId");

        var schema = new ParquetSchema(
            orderIdField,
            customerIdField,
            productIdField,
            orderDateField,
            quantityField,
            unitPriceField,
            regionIdField);

        Console.WriteLine($"Writing {options.Rows:N0} fake orders to {path}");
        Console.WriteLine($"Row group size: {options.RowGroupSize:N0}");

        var stopwatch = Stopwatch.StartNew();
        await using FileStream stream = File.Create(path);
        await using ParquetWriter writer = await ParquetWriter.CreateAsync(schema, stream);

        long rowsWritten = 0;
        int rowGroupNumber = 0;
        while (rowsWritten < options.Rows)
        {
            int batchSize = (int)Math.Min(options.RowGroupSize, options.Rows - rowsWritten);
            OrderBatch batch = CreateBatch(rowsWritten, batchSize);

            using ParquetRowGroupWriter rowGroupWriter = writer.CreateRowGroup();
            await rowGroupWriter.WriteAsync<long>(orderIdField, batch.OrderIds);
            await rowGroupWriter.WriteAsync<int>(customerIdField, batch.CustomerIds);
            await rowGroupWriter.WriteAsync<int>(productIdField, batch.ProductIds);
            await rowGroupWriter.WriteAsync<DateTime>(orderDateField, batch.OrderDatesUtc);
            await rowGroupWriter.WriteAsync<int>(quantityField, batch.Quantities);
            await rowGroupWriter.WriteAsync<double>(unitPriceField, batch.UnitPrices);
            await rowGroupWriter.WriteAsync<byte>(regionIdField, batch.RegionIds);

            rowsWritten += batchSize;
            rowGroupNumber++;
            Console.WriteLine(
                $"Row group {rowGroupNumber:N0}: wrote {rowsWritten:N0}/{options.Rows:N0} rows in {stopwatch.Elapsed}");
        }

        stopwatch.Stop();
        Console.WriteLine($"Finished writing {rowsWritten:N0} rows in {stopwatch.Elapsed}");
        Console.WriteLine($"File size: {DisplayFormatting.FormatBytes(new FileInfo(path).Length)}");
        return 0;
    }

    private static OrderBatch CreateBatch(long startOrderId, int count)
    {
        long[] orderIds = new long[count];
        int[] customerIds = new int[count];
        int[] productIds = new int[count];
        DateTime[] orderDatesUtc = new DateTime[count];
        int[] quantities = new int[count];
        double[] unitPrices = new double[count];
        byte[] regionIds = new byte[count];

        var startDate = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < count; i++)
        {
            long orderId = startOrderId + i + 1;
            ulong hash = Mix((ulong)orderId);

            orderIds[i] = orderId;
            customerIds[i] = (int)(hash % 2_000_000) + 1;
            productIds[i] = (int)((hash >> 11) % 50_000) + 1;
            orderDatesUtc[i] = startDate.AddMinutes((long)((hash >> 23) % (365L * 24 * 60 * 4)));
            quantities[i] = (int)((hash >> 37) % 10) + 1;
            unitPrices[i] = Math.Round(5.0 + ((hash >> 43) % 49_500) / 100.0, 2);
            regionIds[i] = (byte)((hash >> 55) % (ulong)AppDefaults.RegionNames.Length);
        }

        return new OrderBatch(orderIds, customerIds, productIds, orderDatesUtc, quantities, unitPrices, regionIds);
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xbf58476d1ce4e5b9UL;
        value ^= value >> 27;
        value *= 0x94d049bb133111ebUL;
        value ^= value >> 31;
        return value;
    }

    private sealed record OrderBatch(
        long[] OrderIds,
        int[] CustomerIds,
        int[] ProductIds,
        DateTime[] OrderDatesUtc,
        int[] Quantities,
        double[] UnitPrices,
        byte[] RegionIds);
}
