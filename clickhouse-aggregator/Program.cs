try
{
    CliOptions options = CliOptions.Parse(args);
    return await new ClickHouseAggregatorApp().RunAsync(options);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

sealed record CliOptions(string Path, string ContainerName, string Image, bool KeepContainer)
{
    public static CliOptions Parse(string[] args)
    {
        string path = AppPaths.DefaultOrdersPath;
        string containerName = "parquet-clickhouse-aggregator";
        string image = "clickhouse/clickhouse-server:24.12";
        bool keepContainer = false;

        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            switch (option)
            {
                case "-h":
                case "--help":
                case "help":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                case "--path":
                    path = ReadValue(args, ref i, option);
                    break;
                case "--container-name":
                    containerName = ReadValue(args, ref i, option);
                    break;
                case "--image":
                    image = ReadValue(args, ref i, option);
                    break;
                case "--keep-container":
                    keepContainer = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {option}");
            }
        }

        return new CliOptions(path, containerName, image, keepContainer);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}");
        }

        index += 1;
        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            ClickHouseAggregator

            Usage:
              dotnet run --project .\clickhouse-aggregator\ClickHouseAggregator.csproj -c Release -- [--path .\data\orders.parquet] [--container-name parquet-clickhouse-aggregator] [--image clickhouse/clickhouse-server:24.12] [--keep-container]

            Options:
              --path            Parquet file path. Defaults to .\data\orders.parquet from repository root.
              --container-name  Docker container name. Defaults to parquet-clickhouse-aggregator.
              --image           ClickHouse Docker image. Defaults to clickhouse/clickhouse-server:24.12.
              --keep-container  Do not remove the container after the run (debugging).
            """);
    }
}

sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

sealed record AggregationResult(long[] OrdersByRegion, double[] RevenueByRegion, long TotalRows, double TotalRevenue);

static class AppPaths
{
    public static string RepositoryRoot { get; } = ResolveRepositoryRoot();
    public static string DefaultOrdersPath { get; } = Path.Combine(RepositoryRoot, "data", "orders.parquet");

    private static string ResolveRepositoryRoot()
    {
        string[] starts = [Environment.CurrentDirectory, AppContext.BaseDirectory];
        foreach (string start in starts)
        {
            string? root = SearchUp(start);
            if (root is not null)
            {
                return root;
            }
        }

        return Environment.CurrentDirectory;
    }

    private static string? SearchUp(string start)
    {
        DirectoryInfo? current = new(Path.GetFullPath(start));
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "dotnet-app"))
                && Directory.Exists(Path.Combine(current.FullName, "rust-aggregator"))
                && Directory.Exists(Path.Combine(current.FullName, "cpp-aggregator")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
