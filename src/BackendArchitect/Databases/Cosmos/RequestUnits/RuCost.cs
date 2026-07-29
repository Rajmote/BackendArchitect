namespace BackendArchitect.Databases.Cosmos.RequestUnits;

// An illustrative pricing model for Cosmos operations. The absolute numbers are NOT Microsoft's real
// figures — the RATIOS are the lesson, and they match the documented rules of thumb:
//
//   * a 1 KB point read is the anchor: 1 RU
//   * a write costs roughly 5x a read, because it must also update EVERY index entry
//   * a query is priced by the work it does (documents examined, partitions touched),
//     NOT by how many documents it returns
//
// In real code you never estimate this — you read `response.RequestCharge`. This model exists so the
// ratios can be seen and tested.
public static class RuCost
{
    public const double PointReadPerKb = 1.0;          // the anchor: 1 KB point read = 1 RU
    public const double WriteBasePerKb = 5.0;          // writes are ~5x reads...
    public const double IndexWritePerProperty = 0.5;   // ...plus the cost of maintaining each index
    public const double QueryPerDocumentExamined = 0.02;
    public const double QueryOverheadPerPartition = 2.0;

    /// <summary>Cheapest operation in Cosmos: you supplied partition key + id.</summary>
    public static double PointRead(double documentSizeKb) => documentSizeKb * PointReadPerKb;

    /// <summary>Writes pay for the document AND for updating every indexed property.</summary>
    public static double Write(double documentSizeKb, int indexedProperties) =>
        documentSizeKb * WriteBasePerKb + indexedProperties * IndexWritePerProperty;

    /// <summary>You pay for the searching, not the finding: documents examined + per-partition overhead.</summary>
    public static double Query(int documentsExamined, int partitionsTouched) =>
        partitionsTouched * QueryOverheadPerPartition + documentsExamined * QueryPerDocumentExamined;
}
