namespace Services.Orders.Options;

public class MongoOptions
{
    public const string DefaultSectionName = "mongo";

    public string ConnectionString { get; set; } = "mongodb://localhost:27018/?replicaSet=rs0";

    public string Database { get; set; } = "orders";

    public string Collection { get; set; } = "Orders";
}
