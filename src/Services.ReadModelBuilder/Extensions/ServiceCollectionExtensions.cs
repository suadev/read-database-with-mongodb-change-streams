using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Options;
using Services.ReadModelBuilder.Repositories;
using Services.ReadModelBuilder.Repositories.Elastic;
using Services.ReadModelBuilder.Repositories.Mongo;
using Services.ReadModelBuilder.Services.ChangeStream;
using Services.ReadModelBuilder.Services.ChangeStream.Orders;
using Services.ReadModelBuilder.Services.Clients;

namespace Services.ReadModelBuilder.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReadModelBuilder(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.DefaultSectionName));
        services.Configure<ElasticOptions>(configuration.GetSection(ElasticOptions.DefaultSectionName));
        services.Configure<SnapshotOptions>(configuration.GetSection(SnapshotOptions.DefaultSectionName));
        services.Configure<ServicesOptions>(configuration.GetSection(ServicesOptions.DefaultSectionName));

        services.AddMemoryCache();

        var servicesOptions = configuration.GetSection(ServicesOptions.DefaultSectionName).Get<ServicesOptions>()
            ?? new ServicesOptions();

        services.AddHttpClient<ICustomerServiceClient, CustomerServiceClient>(client =>
        {
            client.BaseAddress = new Uri(servicesOptions.Customer?.BaseUrl ?? "http://localhost:5101");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
        {
            client.BaseAddress = new Uri(servicesOptions.Product?.BaseUrl ?? "http://localhost:5102");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IMongoClient>(sp =>
        {
            var mongoOptions = configuration.GetSection(MongoOptions.DefaultSectionName).Get<MongoOptions>()
                ?? throw new InvalidOperationException("Missing 'mongo' configuration section.");
            if (string.IsNullOrWhiteSpace(mongoOptions.ConnectionString))
            {
                throw new InvalidOperationException("'mongo:connectionString' must be set.");
            }
            return new MongoClient(mongoOptions.ConnectionString);
        });

        services.AddSingleton(sp =>
        {
            var elasticOptions = configuration.GetSection(ElasticOptions.DefaultSectionName).Get<ElasticOptions>()
                ?? throw new InvalidOperationException("Missing 'elastic' configuration section.");

            var settings = new ElasticsearchClientSettings(new Uri(elasticOptions.Url))
                .DefaultIndex(elasticOptions.OrderDetailsIndexName);

            return new ElasticsearchClient(settings);
        });

        // Source-side (MongoDB) repositories
        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddTransient<IOrderDetailsSnapshotRepository, OrderDetailsSnapshotRepository>();
        services.AddTransient<IResumeTokenRepository, ResumeTokenRepository>();

        // Read-model (Elasticsearch) repository
        services.AddTransient<IOrderDetailRepository, OrderDetailRepository>();

        // Change-stream pipeline
        services.AddScoped<IChangeStreamEventHandler<Order>, OrderChangeStreamHandler>();
        services.AddScoped<IChangeStreamService<Order>, OrderChangeStreamService>();

        // Snapshot + enricher
        services.AddScoped<ISnapshotService<OrderDetailsSnapshot>, OrderSnapshotService>();
        services.AddScoped<IOrderEnricherService, OrderEnricherService>();

        services.AddHostedService<OrderChangeStreamHostedService>();

        return services;
    }
}
