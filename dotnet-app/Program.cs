using System.Globalization;

try
{
    if (args.Length == 0 || IsHelp(args[0]))
    {
        PrintUsage();
        return 0;
    }

    CliOptions options = CliOptions.Parse(args);

    return options.Command switch
    {
        "generate" => await new OrderDataGenerator().GenerateAsync(options),
        "aggregate" => await new OrderAggregator().AggregateAsync(options),
        _ => PrintUnknownCommand(options.Command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static bool IsHelp(string value) =>
    value is "-h" or "--help" or "help";

static int PrintUnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        ParquetPerformance

        Usage:
          dotnet run -c Release -- generate [--rows 100000000] [--path orders.parquet] [--row-group-size 1000000]
          dotnet run -c Release -- aggregate [--path orders.parquet]

        Commands:
          generate    Create deterministic fake orders and write them to a local Parquet file.
          aggregate   Read the Parquet file, aggregate revenue and order count by region, and time the run.

        Options:
          --rows            Number of fake rows to generate. Defaults to 100,000,000.
          --path            Parquet file path. Defaults to orders.parquet in the current folder.
          --row-group-size  Rows per Parquet row group during generation. Defaults to 1,000,000.
        """);
}

sealed record CliOptions(
    string Command,
    long Rows,
    string Path,
    int RowGroupSize)
{
    public static CliOptions Parse(string[] args)
    {
        string command = args[0].ToLowerInvariant();
        long rows = AppDefaults.Rows;
        string path = AppPaths.DefaultOrdersPath;
        int rowGroupSize = AppDefaults.RowGroupSize;

        for (int i = 1; i < args.Length; i++)
        {
            string option = args[i];
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {option}");
            }

            string value = args[++i];
            switch (option)
            {
                case "--rows":
                    rows = ParsePositiveLong(value, option);
                    break;
                case "--path":
                    path = value;
                    break;
                case "--row-group-size":
                    rowGroupSize = checked((int)ParsePositiveLong(value, option));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {option}");
            }
        }

        return new CliOptions(command, rows, path, rowGroupSize);
    }

    private static long ParsePositiveLong(string value, string option)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) || result <= 0)
        {
            throw new ArgumentException($"{option} must be a positive integer.");
        }

        return result;
    }
}
