using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Order.Core.Orders;
using Polly;

namespace Order.Infrastructure.Orders;

public class OrderSqliteRepository(string connectionString) : IOrderRepository
{
    private const string CreateOrdersTable = @"
            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Brand TEXT NOT NULL,
                Variant TEXT NOT NULL,
                NetContent TEXT NOT NULL,
                OrderNeed TEXT NOT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT (DATETIME('now'))
            );";

    private const string CreateFileLinksTable = @"
            CREATE TABLE IF NOT EXISTS FileLinks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                Url TEXT NOT NULL,
                Processed BOOLEAN NOT NULL DEFAULT 0,
                FOREIGN KEY (OrderId) REFERENCES Orders (Id)
            );";

    private const string InsertOrder = @"
            INSERT INTO Orders (Brand, Variant, NetContent, OrderNeed)
            VALUES (@Brand, @Variant, @NetContent, @OrderNeed);
            SELECT last_insert_rowid();";

    private const string InsertFileLink = @"
            INSERT INTO FileLinks (OrderId, Url, Processed)
            VALUES (@OrderId, @Url, 0);";

    private const string GetFileLinks = @"
            SELECT FileLinks.Id, FileLinks.OrderId, FileLinks.Url,
                   Orders.Brand, Orders.Variant, FileLinks.Processed
            FROM FileLinks
            INNER JOIN Orders ON FileLinks.OrderId = Orders.Id
            WHERE FileLinks.Processed = 0;";

    private const string UpdateFileLink = @"
            UPDATE FileLinks
            SET Processed = 1
            WHERE Id = @Id;";

    private const int Locked = 5;

    private readonly Policy _retryPolicy = Policy
        .Handle<SqliteException>(ex => ex.SqliteErrorCode == Locked)
        .WaitAndRetry(
            retryCount: 5,
            sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(100 * retryAttempt));


    private IDbConnection CreateConnection() => new SqliteConnection(connectionString);

    public Task InitializeSchema(CancellationToken cancellationToken)
    {
        using var connection = CreateConnection();
        connection.Execute("PRAGMA journal_mode=WAL;");
        connection.Execute(CreateOrdersTable);
        connection.Execute(CreateFileLinksTable);
        return Task.CompletedTask;
    }

    public Task<long> Add(Core.Orders.Order order, CancellationToken cancellationToken)
    {
        return Task.FromResult(_retryPolicy.Execute(() =>
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var orderId = connection.ExecuteScalar<long>(InsertOrder, new
                {
                    order.Brand,
                    order.Variant,
                    order.NetContent,
                    order.OrderNeed
                }, transaction);

                foreach (var fileLink in order.FileLinks)
                {
                    connection.Execute(InsertFileLink, new { OrderId = orderId, Url = fileLink }, transaction);
                }

                transaction.Commit();
                return orderId; 
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }));
    }

    public Task<IList<FileLink>> UnprocessedFileLinks(CancellationToken cancellationToken)
    {
        var result = _retryPolicy.Execute(() =>
        {
            using var connection = new SqliteConnection(connectionString);
            return connection.Query<dynamic>(GetFileLinks)
                .Select(row => new FileLink(
                    Id: (long)row.Id,
                    OrderId: (long)row.OrderId,
                    Url: (string)row.Url,
                    Brand: (string)row.Brand,
                    Variant: (string)row.Variant,
                    Processed: (long)row.Processed == 1
                )).ToList();
        });

        return Task.FromResult<IList<FileLink>>(result);
    }

    public Task ProcessFileLink(long id, CancellationToken cancellationToken)
    {
        _retryPolicy.Execute(() =>
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Execute(UpdateFileLink, new { Id = id });
        });

        return Task.CompletedTask;
    }
}