using System.ComponentModel.DataAnnotations;

namespace Order.Core.Orders;

public record Order(
    long Id,
    [Required]
    [MaxLength(100)]
    string Brand,
    [Required]
    [MaxLength(100)]
    string Variant,
    [Required]
    string NetContent,
    [Required]
    string OrderNeed,
    [Required]
    [MinLength(1)]
    List<string> FileLinks,
    DateTime CreatedAt);