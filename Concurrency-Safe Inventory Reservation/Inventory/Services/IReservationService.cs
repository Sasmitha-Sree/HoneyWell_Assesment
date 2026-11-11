using Concurrency_Safe_Inventory_Reservation.Inventory.Models;

namespace Concurrency_Safe_Inventory_Reservation.Inventory.Services;

public interface IReservationService
{
    /// <summary>
    /// Reserves <paramref name="quantity"/> units of <paramref name="sku"/>.
    /// Repeating the same <paramref name="idempotencyKey"/> returns the original outcome
    /// without decrementing stock again.
    /// </summary>
    Task<ReservationResult> ReserveAsync(string sku, int quantity, string idempotencyKey, CancellationToken ct);
}
