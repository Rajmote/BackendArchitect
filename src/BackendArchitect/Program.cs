using BackendArchitect.Databases.Sql.Indexing;
using BackendArchitect.Databases.Sql.Transactions;
using BackendArchitect.Databases.Sql.IsolationLevels;
using BackendArchitect.Databases.Sql.DataModeling;
using BackendArchitect.Databases.NoSql.Concepts;

Console.WriteLine("===== Databases · SQL · Indexing (seek vs scan) =====");
new IndexingDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · SQL · Transactions (the four ACID properties) =====");
new AcidDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · SQL · Isolation levels (oversell: dial off vs on) =====");
new IsolationLevelsDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · SQL · Data modeling (flat vs normalized) =====");
new DataModelingDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · NoSQL · Concepts (normalized vs document) =====");
new NoSqlConceptsDemo().Run();
