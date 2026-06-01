sealed record CliOptions(string Path)
{
    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliOptions(AppPaths.DefaultOrdersPath);
        }

        string first = args[0];
        if (first is "-h" or "--help" or "help")
        {
            PrintUsage();
            Environment.Exit(0);
        }

        if (first != "--path")
        {
            throw new ArgumentException($"Unknown argument: {first}");
        }

        if (args.Length < 2)
        {
            throw new ArgumentException("Missing value for --path");
        }

        if (args.Length > 2)
        {
            throw new ArgumentException("Unexpected extra arguments");
        }

        return new CliOptions(args[1]);
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            .NET DuckDB Parquet Aggregator

            Usage:
              dotnet run -c Release
              dotnet run -c Release -- --path C:\path\to\orders.parquet

            When --path is omitted, the app reads .\data\orders.parquet from the repository root.
            """);
    }
}
