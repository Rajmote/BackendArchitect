using BackendArchitect.Databases.Sql.Indexing;
using BackendArchitect.Databases.Sql.Transactions;
using BackendArchitect.Databases.Sql.IsolationLevels;
using BackendArchitect.Databases.Sql.DataModeling;
using BackendArchitect.Databases.NoSql.Concepts;
using BackendArchitect.Databases.Cosmos.PartitionKeys;
using BackendArchitect.Databases.Cosmos.RequestUnits;
using BackendArchitect.Databases.Cosmos.Indexing;
using BackendArchitect.Databases.Cosmos.Consistency;

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

Console.WriteLine();
Console.WriteLine("===== Databases · Cosmos · Partition keys (spread + query cost) =====");
new PartitionKeysDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · Cosmos · Request Units (cost + throttling) =====");
new RequestUnitsDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · Cosmos · Indexing policy (right-sizing) =====");
new IndexingPolicyDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Databases · Cosmos · Consistency levels (what each read sees) =====");
new ConsistencyLevelsDemo().Run();
