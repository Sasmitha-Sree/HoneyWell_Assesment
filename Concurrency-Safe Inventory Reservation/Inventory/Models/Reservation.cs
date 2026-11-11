namespace Concurrency_Safe_Inventory_Reservation.Inventory.Models;

/// <summary>
/// A persisted reservation. <see cref="IdempotencyKey"/> has a UNIQUE index so that
/// a duplicate request can never create a second reservation.
/// </summary>
public class Reservation
{
    public Guid Id { get; set; }

    public string IdempotencyKey { get; set; } = default!;

    public string Sku { get; set; } = default!;

    public int Quantity { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
