using BackendArchitect.Databases.Sql.Indexing;
using BackendArchitect.Databases.Sql.Transactions;
using BackendArchitect.Databases.Sql.IsolationLevels;

Console.WriteLine("===== Databases · SQL · Indexing (seek vs scan) =====");
new IndexingDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · SQL · Transactions (the four ACID properties) =====");
new AcidDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · SQL · Isolation levels (oversell: dial off vs on) =====");
new IsolationLevelsDemo().Run();
