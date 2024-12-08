namespace Order.Core.Orders;

public record Order(
    long Id,
    string Brand,
    string Variant,
    string NetContent,
    string OrderNeed,
    List<string> FileLinks,
    DateTime CreatedAt);