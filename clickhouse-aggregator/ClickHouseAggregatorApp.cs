using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

sealed class ClickHouseAggregatorApp
{
    private static readonly string[] RegionNames = ["North", "South", "East", "West", "Central", "Online"];

    public async Task<int> RunAsync(CliOptions options)
    {
        string parquetPath = Path.GetFullPath(options.Path);
        if (!File.Exists(parquetPath))
        {
            throw new FileNotFoundException($"Parquet file not found: {parquetPath}");
        }

        string dataDirectory = Path.GetFullPath(Path.Combine(AppPaths.RepositoryRoot, "data"));
        string relativeParquetPath = Path.GetRelativePath(dataDirectory, parquetPath);
        if (relativeParquetPath.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The parquet file must be inside {dataDirectory} so Docker can read it via the mapped volume.");
        }

        string containerParquetPath = $"data/{relativeParquetPath.Replace('\\', '/')}";

        await EnsureDockerRunningAsync();
        await RemoveContainerIfExistsAsync(options.ContainerName);

        string mountValue = $"type=bind,source={dataDirectory},target=/var/lib/clickhouse/user_files/data";
        await RunDockerAsync(["run", "--detach", "--rm", "--name", options.ContainerName, "--mount", mountValue, options.Image]);

        try
        {
            await WaitForReadyAsync(options.ContainerName);

            FileInfo fileInfo = new(parquetPath);
            Console.WriteLine($"Reading {parquetPath}");
            Console.WriteLine($"File size: {Format.Bytes(fileInfo.Length)}");
            Console.WriteLine($"Container: {options.ContainerName} ({options.Image})");
            Console.WriteLine();

            string sql = BuildSql(containerParquetPath);
            Stopwatch stopwatch = Stopwatch.StartNew();
            ProcessResult queryResult = await RunDockerAsync(
                ["exec", "-i", options.ContainerName, "clickhouse-client", "--multiquery"],
                stdIn: sql);
            stopwatch.Stop();

            AggregationResult aggregation = ParseQueryOutput(queryResult.StdOut);

            PrintAggregation(aggregation);

            double elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
            long throughput = (long)Math.Round(aggregation.TotalRows / elapsedSeconds, MidpointRounding.AwayFromZero);

            Console.WriteLine($"Processed {Format.Count(aggregation.TotalRows)} rows in {Format.Elapsed(stopwatch.Elapsed)}");
            Console.WriteLine($"Throughput: {Format.Count(throughput)} rows/sec");

            return 0;
        }
        finally
        {
            if (!options.KeepContainer)
            {
                await RemoveContainerIfExistsAsync(options.ContainerName);
            }
        }
    }

    private static async Task EnsureDockerRunningAsync()
    {
        ProcessResult result = await RunDockerAsync(["version", "--format", "{{.Server.Version}}"]);
        if (string.IsNullOrWhiteSpace(result.StdOut.Trim()))
        {
            throw new InvalidOperationException("Docker Desktop is required and the Docker daemon must be running.");
        }
    }

    private static async Task WaitForReadyAsync(string containerName)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            ProcessResult probe = await RunDockerAsync(
                ["exec", containerName, "clickhouse-client", "--query", "SELECT 1 FORMAT TabSeparatedRaw"],
                allowFailure: true);

            if (probe.ExitCode == 0 && probe.StdOut.Trim() == "1")
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("ClickHouse container did not become ready in time.");
    }

    private static async Task RemoveContainerIfExistsAsync(string containerName)
    {
        ProcessResult existing = await RunDockerAsync(
            ["ps", "-a", "--filter", $"name=^/{containerName}$", "--format", "{{.ID}}"]);

        if (!string.IsNullOrWhiteSpace(existing.StdOut))
        {
            await RunDockerAsync(["rm", "-f", containerName], allowFailure: true);
        }
    }

    private static string BuildSql(string containerParquetPath)
    {
        return
            $$"""
            CREATE TEMPORARY TABLE orders_tmp
            (
                Quantity UInt32,
                UnitPrice Float64,
                RegionId UInt32
            );

            INSERT INTO orders_tmp
            SELECT
                toUInt32(Quantity),
                toFloat64(UnitPrice),
                toUInt32(RegionId)
            FROM file('{{containerParquetPath}}', Parquet);

            SELECT
                toInt32(RegionId) % {{RegionNames.Length}} AS region_index,
                count() AS orders,
                sum(Quantity * UnitPrice) AS revenue
            FROM orders_tmp
            GROUP BY region_index
            ORDER BY region_index
            FORMAT JSONEachRow;

            SELECT
                count() AS total_rows,
                sum(Quantity * UnitPrice) AS total_revenue
            FROM orders_tmp
            FORMAT JSONEachRow;
            """;
    }

    private static AggregationResult ParseQueryOutput(string stdOut)
    {
        long[] ordersByRegion = new long[RegionNames.Length];
        double[] revenueByRegion = new double[RegionNames.Length];
        long totalRows = 0;
        double totalRevenue = 0;

        string[] lines = stdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string line in lines)
        {
            using JsonDocument json = JsonDocument.Parse(line);
            JsonElement root = json.RootElement;

            if (root.TryGetProperty("region_index", out JsonElement regionIndexElement))
            {
                int regionIndex = regionIndexElement.GetInt32();
                if ((uint)regionIndex < RegionNames.Length)
                {
                    ordersByRegion[regionIndex] = ReadInt64(root.GetProperty("orders"));
                    revenueByRegion[regionIndex] = ReadDouble(root.GetProperty("revenue"));
                }

                continue;
            }

            if (root.TryGetProperty("total_rows", out JsonElement totalRowsElement))
            {
                totalRows = ReadInt64(totalRowsElement);
                totalRevenue = ReadDouble(root.GetProperty("total_revenue"));
            }
        }

        if (totalRows == 0)
        {
            for (int i = 0; i < RegionNames.Length; i++)
            {
                totalRows += ordersByRegion[i];
                totalRevenue += revenueByRegion[i];
            }
        }

        return new AggregationResult(ordersByRegion, revenueByRegion, totalRows, totalRevenue);
    }

    private static long ReadInt64(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt64(),
            JsonValueKind.String when long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) => value,
            _ => throw new FormatException($"Expected integer value but got {element.ValueKind}.")
        };
    }

    private static double ReadDouble(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double value) => value,
            _ => throw new FormatException($"Expected floating-point value but got {element.ValueKind}.")
        };
    }

    private static void PrintAggregation(AggregationResult aggregation)
    {
        Console.WriteLine("Aggregation by region");
        Console.WriteLine("Region       Orders            Revenue");
        Console.WriteLine("----------------------------------------------");

        for (int i = 0; i < RegionNames.Length; i++)
        {
            string region = RegionNames[i].PadRight(10);
            string orders = Format.Count(aggregation.OrdersByRegion[i]).PadLeft(14);
            string revenue = Format.Currency(aggregation.RevenueByRegion[i]).PadLeft(18);
            Console.WriteLine($"{region} {orders} {revenue}");
        }

        Console.WriteLine("----------------------------------------------");
        Console.WriteLine($"{"Total".PadRight(10)} {Format.Count(aggregation.TotalRows).PadLeft(14)} {Format.Currency(aggregation.TotalRevenue).PadLeft(18)}");
        Console.WriteLine();
    }

    private static async Task<ProcessResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        string? stdIn = null,
        bool allowFailure = false)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdIn is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = psi };
        process.Start();

        if (stdIn is not null)
        {
            await process.StandardInput.WriteAsync(stdIn);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
        }

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        string stdOut = await stdOutTask;
        string stdErr = await stdErrTask;

        ProcessResult result = new(process.ExitCode, stdOut, stdErr);
        if (!allowFailure && result.ExitCode != 0)
        {
            string renderedArgs = string.Join(' ', arguments);
            string message = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
            throw new InvalidOperationException($"docker {renderedArgs} failed: {message.Trim()}");
        }

        return result;
    }
}
