using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Services.Products.Endpoints;
using Services.Products.Options;
using Services.Products.Repositories;

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.DefaultSectionName));
builder.Services.AddSingleton<IMongoClient>(_ =>
{
    var cs = builder.Configuration[$"{MongoOptions.DefaultSectionName}:connectionString"];
    return new MongoClient(cs);
});
builder.Services.AddTransient<IProductRepository, ProductRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapProductEndpoints();

app.Run();
