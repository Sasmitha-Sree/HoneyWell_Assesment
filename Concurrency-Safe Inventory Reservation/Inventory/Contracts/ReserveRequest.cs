using System.ComponentModel.DataAnnotations;

namespace Concurrency_Safe_Inventory_Reservation.Inventory.Contracts;

public sealed class ReserveRequest
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string Sku { get; set; } = default!;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
