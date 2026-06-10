sealed record AggregationResult(
    string FullPath,
    AggregationSource Source,
    long FileSize,
    int ThreadCount,
    long RowGroupCount,
    long ExpectedRows,
    TimeSpan? LoadElapsed,
    long[] OrdersByRegion,
    double[] RevenueByRegion,
    long RowsRead,
    double TotalRevenue,
    TimeSpan Elapsed);
