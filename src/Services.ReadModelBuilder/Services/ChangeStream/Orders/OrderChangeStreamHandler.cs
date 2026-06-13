using MongoDB.Driver;
using Services.ReadModelBuilder.Domain;
using Services.ReadModelBuilder.Helpers;
using Services.ReadModelBuilder.Repositories;

namespace Services.ReadModelBuilder.Services.ChangeStream.Orders;

public class OrderChangeStreamHandler : IChangeStreamEventHandler<Order>
{
    private readonly IOrderDetailRepository _orderDetailRepository;
    private readonly IOrderEnricherService _enricherService;
    private readonly ILogger<OrderChangeStreamHandler> _logger;

    public OrderChangeStreamHandler(
        IOrderDetailRepository orderDetailRepository,
        IOrderEnricherService enricherService,
        ILogger<OrderChangeStreamHandler> logger)
    {
        _orderDetailRepository = orderDetailRepository;
        _enricherService = enricherService;
        _logger = logger;
    }

    public async Task HandleAsync(ChangeStreamDocument<Order> changeDocument, CancellationToken cancellationToken = default)
    {
        Guid documentId = Guid.Empty;
        try
        {
            documentId = changeDocument.DocumentKey["_id"].ToGuid();

            switch (changeDocument.OperationType)
            {
                case ChangeStreamOperationType.Insert:
                    await HandleInsertAsync(documentId, changeDocument, cancellationToken);
                    break;
                case ChangeStreamOperationType.Update:
                    await HandleUpdateAsync(documentId, changeDocument, cancellationToken);
                    break;
                case ChangeStreamOperationType.Replace:
                    await HandleReplaceAsync(documentId, changeDocument, cancellationToken);
                    break;
                case ChangeStreamOperationType.Delete:
                    await HandleDeleteAsync(documentId, cancellationToken);
                    break;
                case ChangeStreamOperationType.Invalidate:
                    _logger.LogCritical("Change stream INVALIDATED. The collection was likely dropped or renamed. Service will restart.");
                    throw new InvalidOperationException("Change stream invalidated — collection may have been dropped or renamed.");
                case ChangeStreamOperationType.Drop:
                case ChangeStreamOperationType.Rename:
                case ChangeStreamOperationType.DropDatabase:
                    _logger.LogWarning("Administrative operation {OperationType} detected on collection. Change stream may invalidate.", changeDocument.OperationType);
                    break;
                default:
                    _logger.LogWarning("Unhandled operation type {OperationType} for Order {OrderId}.", changeDocument.OperationType, documentId);
                    break;
            }
        }
        catch (Exception ex)
        {
            var documentKeyInfo = documentId != Guid.Empty
                ? documentId.ToString()
                : $"DocumentKey: {changeDocument.DocumentKey?.ToString() ?? "null"}";

            _logger.LogError(ex, "Error processing change stream event for Order {DocumentInfo} with operation {OperationType}.",
                documentKeyInfo, changeDocument.OperationType);
            throw;
        }
    }

    private async Task HandleInsertAsync(Guid documentId, ChangeStreamDocument<Order> changeDocument, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling INSERT operation for Order {OrderId}.", documentId);

        var order = changeDocument.FullDocument;
        if (order == null)
        {
            _logger.LogWarning("Full document is null for INSERT operation on Order {OrderId}.", documentId);
            return;
        }

        var orderDetails = await _enricherService.EnrichAllAsync([order], cancellationToken);
        var orderDetail = orderDetails.Single();

        orderDetail.LocalCreatedAt = DateTime.UtcNow;
        orderDetail.LocalUpdatedAt = DateTime.UtcNow;

        await _orderDetailRepository.UpsertAsync(orderDetail, cancellationToken);
        _logger.LogInformation("Successfully upserted OrderDetail for Order {OrderId}.", documentId);
    }

    private async Task HandleUpdateAsync(Guid documentId, ChangeStreamDocument<Order> changeDocument, CancellationToken cancellationToken)
    {
        var updatedFields = changeDocument.UpdateDescription?.UpdatedFields.GetUpdatedFieldNames().ToList() ?? [];
        _logger.LogInformation("Handling UPDATE operation for Order {OrderId}. Updated fields: {UpdatedFields}.", documentId, string.Join(", ", updatedFields));

        var existingDocument = await _orderDetailRepository.FindByIdAsync(documentId, cancellationToken);

        if (existingDocument == null)
        {
            // Document doesn't exist — treat update as upsert. Common right after a resume-token reset.
            _logger.LogInformation("OrderDetail document not found for Order {OrderId}; treating UPDATE as full upsert.", documentId);

            var order = changeDocument.FullDocument;
            if (order == null)
            {
                _logger.LogWarning("Full document is null for UPDATE (upsert) operation on Order {OrderId}.", documentId);
                return;
            }

            var orderDetails = await _enricherService.EnrichAllAsync([order], cancellationToken);
            var orderDetail = orderDetails.Single();

            orderDetail.LocalCreatedAt = DateTime.UtcNow;
            orderDetail.LocalUpdatedAt = DateTime.UtcNow;

            await _orderDetailRepository.UpsertAsync(orderDetail, cancellationToken);
            _logger.LogInformation("Successfully created OrderDetail for Order {OrderId} during update operation.", documentId);
            return;
        }

        var order2 = changeDocument.FullDocument;
        if (order2 == null)
        {
            _logger.LogWarning("Full document is null for UPDATE on Order {OrderId}; partial update skipped.", documentId);
            return;
        }

        var partial = new Dictionary<string, object>();
        foreach (var fieldName in updatedFields)
        {
            if (OrderFieldMappings.FieldMappings.TryGetValue(fieldName, out var mapper))
            {
                var kvp = mapper(order2);
                partial[kvp.Key] = kvp.Value;
            }
        }

        if (partial.Count == 0)
        {
            _logger.LogInformation("No applicable fields to update in OrderDetail for Order {OrderId}.", documentId);
            return;
        }

        partial[nameof(OrderDetail.LocalUpdatedAt)] = DateTime.UtcNow;

        var matched = await _orderDetailRepository.PartialUpdateAsync(documentId, partial, cancellationToken);
        if (!matched)
        {
            _logger.LogWarning("OrderDetail document was missing during partial update for Order {OrderId} (race with delete?).", documentId);
            return;
        }

        _logger.LogInformation("Successfully updated OrderDetail for Order {OrderId}. Updated {FieldCount} fields.", documentId, partial.Count - 1);
    }

    private async Task HandleReplaceAsync(Guid documentId, ChangeStreamDocument<Order> changeDocument, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling REPLACE operation for Order {OrderId}.", documentId);

        var order = changeDocument.FullDocument;
        if (order == null)
        {
            _logger.LogWarning("Full document is null for REPLACE operation on Order {OrderId}.", documentId);
            return;
        }

        var existingDocument = await _orderDetailRepository.FindByIdAsync(documentId, cancellationToken);

        var orderDetails = await _enricherService.EnrichAllAsync([order], cancellationToken);
        var orderDetail = orderDetails.Single();

        orderDetail.LocalCreatedAt = existingDocument != null ? existingDocument.LocalCreatedAt : DateTime.UtcNow;
        orderDetail.LocalUpdatedAt = DateTime.UtcNow;

        await _orderDetailRepository.UpsertAsync(orderDetail, cancellationToken);
        _logger.LogInformation("Successfully replaced OrderDetail for Order {OrderId}.", documentId);
    }

    private async Task HandleDeleteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling DELETE operation for Order {OrderId}.", documentId);

        var deleted = await _orderDetailRepository.DeleteAsync(documentId, cancellationToken);
        if (!deleted)
        {
            _logger.LogWarning("OrderDetail document not found for deletion, Order {OrderId}.", documentId);
        }
        else
        {
            _logger.LogInformation("Successfully deleted OrderDetail for Order {OrderId}.", documentId);
        }
    }
}
