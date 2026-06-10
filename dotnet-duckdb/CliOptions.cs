enum AggregationSource
{
    File,
    Memory
}

sealed record CliOptions(string Path, AggregationSource Source)
{
    public static CliOptions Parse(string[] args)
    {
        string path = AppPaths.DefaultOrdersPath;
        AggregationSource source = AggregationSource.File;

        if (args.Length == 0)
        {
            return new CliOptions(path, source);
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "-h" or "--help" or "help")
            {
                PrintUsage();
                Environment.Exit(0);
            }

            if (arg == "--path")
            {
                if (++i >= args.Length)
                {
                    throw new ArgumentException("Missing value for --path");
                }

                path = args[i];
                continue;
            }

            if (arg == "--source")
            {
                if (++i >= args.Length)
                {
                    throw new ArgumentException("Missing value for --source");
                }

                source = ParseSource(args[i]);
                continue;
            }

            throw new ArgumentException($"Unknown argument: {arg}");
        }

        return new CliOptions(path, source);
    }

    private static AggregationSource ParseSource(string value) =>
        value.ToLowerInvariant() switch
        {
            "file" or "parquet" => AggregationSource.File,
            "memory" or "mem" => AggregationSource.Memory,
            _ => throw new ArgumentException($"Unknown --source value: {value}. Expected file or memory.")
        };

    public string SourceDescription => Source switch
    {
        AggregationSource.File => "Parquet file",
        AggregationSource.Memory => "DuckDB in-memory table",
        _ => Source.ToString()
    };

    public string TimedWorkDescription => Source switch
    {
        AggregationSource.File => "read_parquet aggregation query",
        AggregationSource.Memory => "aggregation query after loading table into memory",
        _ => "aggregation query"
    };

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            .NET DuckDB Parquet Aggregator

            Usage:
              dotnet run -c Release
              dotnet run -c Release -- --path C:\path\to\orders.parquet
              dotnet run -c Release -- --source memory --path C:\path\to\orders.parquet

            When --path is omitted, the app reads .\data\orders.parquet from the repository root.
            --source file reads from the Parquet file during the aggregation query.
            --source memory loads the Parquet file into a DuckDB in-memory table before timing,
            then reports only the aggregation query time.
            """);
    }
}
