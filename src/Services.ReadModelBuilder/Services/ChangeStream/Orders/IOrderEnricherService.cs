using Services.ReadModelBuilder.Domain;

namespace Services.ReadModelBuilder.Services.ChangeStream.Orders;

public interface IOrderEnricherService : IEnricherService<Order, OrderDetail>
{
}
