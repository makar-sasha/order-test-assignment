namespace Order.Core.Orders;

public record FileLink(
    long Id,
    long OrderId,
    string Url,
    string Brand,
    string Variant,
    bool Processed);