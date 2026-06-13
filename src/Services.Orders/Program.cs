using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Services.Orders.Endpoints;
using Services.Orders.Options;
using Services.Orders.Repositories;
using Services.Orders.Services.Clients;

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.DefaultSectionName));
builder.Services.Configure<ServicesOptions>(builder.Configuration.GetSection(ServicesOptions.DefaultSectionName));

builder.Services.AddSingleton<IMongoClient>(_ =>
{
    var cs = builder.Configuration[$"{MongoOptions.DefaultSectionName}:connectionString"];
    return new MongoClient(cs);
});
builder.Services.AddTransient<IOrderRepository, OrderRepository>();

var servicesOptions = builder.Configuration.GetSection(ServicesOptions.DefaultSectionName).Get<ServicesOptions>()
    ?? new ServicesOptions();

builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    client.BaseAddress = new Uri(servicesOptions.Product?.BaseUrl ?? "http://localhost:5102");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapOrderEndpoints();

app.Run();
