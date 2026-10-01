var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql").WithDataVolume();
var ticketFlowDb = sql.AddDatabase("DefaultConnection", "TicketFlow");

var redis = builder.AddRedis("redis");

var rabbitMq = builder.AddRabbitMQ("rabbitMq");
builder
    .AddProject<Projects.TicketFlow_Presentation>("api")
    .WithReference(ticketFlowDb)
    .WaitFor(ticketFlowDb)
    .WithEnvironment("Redis__ConnectionString", redis.Resource.ConnectionStringExpression)
    .WithReference(rabbitMq);


builder.Build().Run();
