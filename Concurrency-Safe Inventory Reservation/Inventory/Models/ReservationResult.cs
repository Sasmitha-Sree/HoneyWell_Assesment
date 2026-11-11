namespace Concurrency_Safe_Inventory_Reservation.Inventory.Models;

public enum ReservationStatus
{
    Reserved,
    AlreadyReserved,
    InsufficientStock,
    SkuNotFound,
    InvalidRequest,
    Cancelled,
    ConcurrencyFailure
}

/// <summary>
/// Outcome of a reservation attempt. Use <see cref="Status"/> to map to an HTTP result.
/// </summary>
public sealed record ReservationResult(
    ReservationStatus Status,
    Guid? ReservationId = null,
    string? Sku = null,
    int Quantity = 0,
    int? RemainingQuantity = null,
    string? Error = null)
{
    public bool IsSuccess => Status is ReservationStatus.Reserved or ReservationStatus.AlreadyReserved;

    public static ReservationResult Reserved(Reservation r, int remaining) =>
 new(ReservationStatus.Reserved, r.Id, r.Sku, r.Quantity, remaining);

    public static ReservationResult AlreadyReserved(Reservation r) =>
        new(ReservationStatus.AlreadyReserved, r.Id, r.Sku, r.Quantity);

    public static ReservationResult InsufficientStock(string sku, int requested, int available) =>
        new(ReservationStatus.InsufficientStock, null, sku, requested, available,
     $"Only {available} unit(s) available for SKU '{sku}'.");

    public static ReservationResult SkuNotFound(string sku) =>
        new(ReservationStatus.SkuNotFound, null, sku, 0, null, $"SKU '{sku}' was not found.");

    public static ReservationResult Invalid(string error) =>
        new(ReservationStatus.InvalidRequest, null, null, 0, null, error);

    public static ReservationResult Cancelled() =>
 new(ReservationStatus.Cancelled, null, null, 0, null, "The operation was cancelled.");

    public static ReservationResult ConcurrencyFailure(string sku) =>
        new(ReservationStatus.ConcurrencyFailure, null, sku, 0, null,
     "The reservation could not be completed due to contention. Please retry.");
}
