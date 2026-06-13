using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Services.Customers.Endpoints;
using Services.Customers.Options;
using Services.Customers.Repositories;

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.DefaultSectionName));
builder.Services.AddSingleton<IMongoClient>(_ =>
{
    var cs = builder.Configuration[$"{MongoOptions.DefaultSectionName}:connectionString"];
    return new MongoClient(cs);
});
builder.Services.AddTransient<ICustomerRepository, CustomerRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapCustomerEndpoints();

app.Run();
