static class AppPaths
{
    public static string DefaultOrdersPath =>
        Path.Combine(FindRepositoryRoot(), AppDefaults.DefaultDataDirectory, AppDefaults.DefaultFileName);

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                bool hasDotnet = Directory.Exists(Path.Combine(directory.FullName, "dotnet-app"));
                bool hasRust = Directory.Exists(Path.Combine(directory.FullName, "rust-aggregator"));
                bool hasCpp = Directory.Exists(Path.Combine(directory.FullName, "cpp-aggregator"));
                if (hasDotnet && hasRust && hasCpp)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Environment.CurrentDirectory;
    }
}
