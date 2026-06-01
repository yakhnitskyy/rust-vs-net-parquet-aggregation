sealed record AggregationResult(
    string FullPath,
    long FileSize,
    int ThreadCount,
    long RowGroupCount,
    long ExpectedRows,
    long[] OrdersByRegion,
    double[] RevenueByRegion,
    long RowsRead,
    double TotalRevenue,
    TimeSpan Elapsed);
