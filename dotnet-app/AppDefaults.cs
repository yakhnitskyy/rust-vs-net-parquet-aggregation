static class AppDefaults
{
    public const string FileName = "orders.parquet";
    public const long Rows = 100_000_000;
    public const int RowGroupSize = 1_000_000;

    public static readonly string[] RegionNames = ["North", "South", "East", "West", "Central", "Online"];
}
