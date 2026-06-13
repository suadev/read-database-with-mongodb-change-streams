using Microsoft.Extensions.Options;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Domain.Constants;
using Services.ReadModelBuilder.Options;
using Services.ReadModelBuilder.Repositories;

namespace Services.ReadModelBuilder.Services.ChangeStream.Orders;

public class OrderChangeStreamService : MongoChangeStreamService<Order>
{
    public OrderChangeStreamService(
        IOrderRepository repository,
        IResumeTokenRepository resumeTokenRepository,
        IChangeStreamEventHandler<Order> changeEventHandler,
        IOptions<MongoOptions> mongoOptions,
        ILogger<MongoChangeStreamService<Order>> logger)
        : base(repository, resumeTokenRepository, changeEventHandler, mongoOptions, logger, MongoConstants.OrderCollectionName)
    {
    }
}
