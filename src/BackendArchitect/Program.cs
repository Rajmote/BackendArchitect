using BackendArchitect.Databases.Sql.Indexing;
using BackendArchitect.Databases.Sql.Transactions;

Console.WriteLine("===== Databases · SQL · Indexing (seek vs scan) =====");
new IndexingDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · SQL · Transactions (the four ACID properties) =====");
new AcidDemo().Run();
