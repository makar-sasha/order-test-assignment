namespace Order.Core.Orders;

public interface IOrderRepository
{
    Task InitializeSchema(CancellationToken cancellationToken);
    Task<long> Add(Order order, CancellationToken cancellationToken);
    Task<IList<FileLink>> UnprocessedFileLinks(CancellationToken cancellationToken);
    Task ProcessFileLink(long id, CancellationToken cancellationToken);
}