namespace Concurrency_Safe_Inventory_Reservation.Inventory.Models;

/// <summary>
/// Stock row for a SKU. <see cref="RowVersion"/> is used for optimistic concurrency.
/// </summary>
public class InventoryItem
{
    public string Sku { get; set; } = default!;

    public int AvailableQuantity { get; set; }

    /// <summary>Optimistic concurrency token (xmin / rowversion / incrementing value).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
