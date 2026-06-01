string[] regionNames = AppDefaults.RegionNames;

try
{
    CliOptions options = CliOptions.Parse(args);
    string fullPath = Path.GetFullPath(options.Path);

    if (!File.Exists(fullPath))
    {
        Console.Error.WriteLine($"Parquet file not found: {fullPath}");
        return 1;
    }

    var aggregator = new DuckDbAggregator();
    var result = aggregator.Aggregate(fullPath, regionNames.Length);

    Console.WriteLine($"Reading {result.FullPath}");
    Console.WriteLine($"File size: {DisplayFormatting.FormatBytes(result.FileSize)}");
    Console.WriteLine($"DuckDB threads: {result.ThreadCount:N0}");
    if (result.RowGroupCount > 0)
    {
        Console.WriteLine($"Row groups: {result.RowGroupCount:N0} (metadata), expected rows: {result.ExpectedRows:N0}");
    }
    Console.WriteLine();
    Console.WriteLine("Aggregation by region");
    Console.WriteLine("Region       Orders            Revenue");
    Console.WriteLine("----------------------------------------------");
    for (int i = 0; i < regionNames.Length; i++)
    {
        Console.WriteLine($"{regionNames[i],-10} {result.OrdersByRegion[i],14:N0} {result.RevenueByRegion[i],18:C2}");
    }

    Console.WriteLine("----------------------------------------------");
    Console.WriteLine($"{"Total",-10} {result.RowsRead,14:N0} {result.TotalRevenue,18:C2}");
    Console.WriteLine();
    Console.WriteLine($"Processed {result.RowsRead:N0} rows in {result.Elapsed}");
    Console.WriteLine($"Throughput: {result.RowsRead / Math.Max(result.Elapsed.TotalSeconds, 0.001):N0} rows/sec");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
