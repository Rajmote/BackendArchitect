using BackendArchitect.Databases.Sql.Indexing;
using BackendArchitect.Databases.Sql.Transactions;
using BackendArchitect.Databases.Sql.IsolationLevels;
using BackendArchitect.Databases.Sql.DataModeling;
using BackendArchitect.Databases.NoSql.Concepts;
using BackendArchitect.Databases.Cosmos.PartitionKeys;
using BackendArchitect.Databases.Cosmos.RequestUnits;
using BackendArchitect.Databases.Cosmos.Indexing;
using BackendArchitect.Databases.Cosmos.Consistency;
using BackendArchitect.Apis.Http.Fundamentals;
using BackendArchitect.Apis.Rest.Design;
using BackendArchitect.Apis.Grpc.Contracts;
using BackendArchitect.Apis.GraphQL.Basics;
using BackendArchitect.Reliability.Resilience;

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

Console.WriteLine();
Console.WriteLine("===== APIs · HTTP · Fundamentals (safe, idempotent, retries) =====");
new HttpFundamentalsDemo().Run();

Console.WriteLine();
Console.WriteLine("===== APIs · REST · Design & versioning =====");
new RestDesignDemo().Run();

Console.WriteLine();
Console.WriteLine("===== APIs · gRPC · Contracts, compatibility & deadlines =====");
new GrpcDemo().Run();

Console.WriteLine();
Console.WriteLine("===== APIs · GraphQL · Basics (fetching, N+1, query limits) =====");
new GraphQlDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Reliability · Resilience (retries, breakers, bulkheads) =====");
new ResilienceDemo().Run();

Console.WriteLine();
Console.WriteLine("===== Reliability · Resilience with Polly (the real library) =====");
new PollyDemo().Run();
