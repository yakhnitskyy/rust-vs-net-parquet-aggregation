static class AppPaths
{
    public static string DefaultOrdersPath =>
        Path.Combine(FindRepositoryRoot(), "data", AppDefaults.FileName);

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "dotnet-app")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "rust-aggregator")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Environment.CurrentDirectory;
    }
}
