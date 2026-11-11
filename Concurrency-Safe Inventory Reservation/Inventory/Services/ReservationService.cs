using Concurrency_Safe_Inventory_Reservation.Inventory.Data;
using Concurrency_Safe_Inventory_Reservation.Inventory.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Concurrency_Safe_Inventory_Reservation.Inventory.Services;

/// <summary>
/// Reservation service built on three guarantees:
/// 1. Idempotency  -> UNIQUE index on Reservation.IdempotencyKey (checked up-front, enforced by the DB).
/// 2. No overselling -> optimistic concurrency token on InventoryItem.RowVersion + bounded retry.
/// 3. Atomicity     -> stock decrement and reservation insert share one serializable transaction.
/// </summary>
public sealed class ReservationService : IReservationService
{
    private const int MaxRetries = 10;
    private const int MaxSkuLength = 64;
    private const int MaxIdempotencyKeyLength = 128;

    private readonly InventoryDbContext _db;
    private readonly ILogger<ReservationService> _logger;
    private readonly TimeProvider _timeProvider;

    public ReservationService(
 InventoryDbContext db,
        ILogger<ReservationService> logger,
 TimeProvider? timeProvider = null)
    {
        _db = db;
 _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ReservationResult> ReserveAsync(string sku, int quantity, string idempotencyKey, CancellationToken ct)
    {
 if (Validate(sku, quantity, idempotencyKey) is { } invalid)
        {
     return invalid;
        }

        sku = sku.Trim();
        idempotencyKey = idempotencyKey.Trim();

        try
        {
     for (var attempt = 1; attempt <= MaxRetries; attempt++)
     {
  ct.ThrowIfCancellationRequested();

         var result = await TryReserveOnceAsync(sku, quantity, idempotencyKey, ct);
  if (result is not null)
         {
      return result;
         }

         _logger.LogWarning(
      "Concurrency conflict reserving {Quantity} of {Sku} (attempt {Attempt}/{MaxRetries}).",
      quantity, sku, attempt, MaxRetries);

         // Small jittered backoff so competing writers do not collide repeatedly.
         await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 25)), ct);
     }

     return ReservationResult.ConcurrencyFailure(sku);
 }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Reservation for {Sku} cancelled by caller.", sku);
     return ReservationResult.Cancelled();
        }
    }

    /// <summary>
    /// Single attempt. Returns <c>null</c> when a concurrency conflict occurred and the caller should retry.
    /// </summary>
    private async Task<ReservationResult?> TryReserveOnceAsync(string sku, int quantity, string idempotencyKey, CancellationToken ct)
    {
 // Fast path: the key was already processed, replay the stored outcome.
        var existing = await _db.Reservations
            .AsNoTracking()
     .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);

 if (existing is not null)
 {
     return ReplayOrConflict(existing, sku, quantity);
        }

        await using IDbContextTransaction tx =
     await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Sku == sku, ct);
 if (item is null)
        {
     return ReservationResult.SkuNotFound(sku);
        }

        if (item.AvailableQuantity < quantity)
 {
     return ReservationResult.InsufficientStock(sku, quantity, item.AvailableQuantity);
        }

        item.AvailableQuantity -= quantity;
        // Rotate the concurrency token so a parallel writer that read the old value fails.
 item.RowVersion = Guid.NewGuid().ToByteArray();

        var reservation = new Reservation
        {
     Id = Guid.NewGuid(),
     IdempotencyKey = idempotencyKey,
     Sku = sku,
     Quantity = quantity,
     CreatedAtUtc = _timeProvider.GetUtcNow()
        };
        _db.Reservations.Add(reservation);

        try
        {
            // One SaveChanges => decrement and insert commit (or roll back) together.
     await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

     return ReservationResult.Reserved(reservation, item.AvailableQuantity);
        }
        catch (DbUpdateConcurrencyException)
        {
     // Another request changed the stock row first.
     await tx.RollbackAsync(CancellationToken.None);
     DetachAll();
     return null;
        }
        catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
        {
     // The same idempotency key won the race; replay its stored outcome.
     await tx.RollbackAsync(CancellationToken.None);
     DetachAll();

     var winner = await _db.Reservations
         .AsNoTracking()
  .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);

     return winner is not null
  ? ReplayOrConflict(winner, sku, quantity)
         : null;
 }
        catch (DbUpdateException)
        {
     // Transient store failure (for example a write lock). Roll back and let the caller retry.
     await tx.RollbackAsync(CancellationToken.None);
     DetachAll();
     return null;
 }
    }

    private static ReservationResult ReplayOrConflict(Reservation existing, string sku, int quantity)
    {
        // Same key reused with a different payload is a client bug, not a replay.
        if (!string.Equals(existing.Sku, sku, StringComparison.Ordinal) || existing.Quantity != quantity)
        {
     return ReservationResult.Invalid(
  $"Idempotency key '{existing.IdempotencyKey}' was already used for a different request.");
        }

        return ReservationResult.AlreadyReserved(existing);
    }

    private static ReservationResult? Validate(string sku, int quantity, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(sku))
     return ReservationResult.Invalid("SKU must not be empty.");

 if (sku.Trim().Length > MaxSkuLength)
     return ReservationResult.Invalid($"SKU must be {MaxSkuLength} characters or fewer.");

        if (quantity <= 0)
     return ReservationResult.Invalid("Quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return ReservationResult.Invalid("Idempotency key must not be empty.");

 if (idempotencyKey.Trim().Length > MaxIdempotencyKeyLength)
     return ReservationResult.Invalid($"Idempotency key must be {MaxIdempotencyKeyLength} characters or fewer.");

        return null;
    }

    private static bool IsUniqueKeyViolation(DbUpdateException ex)
    {
 var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
     || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private void DetachAll()
    {
        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
     entry.State = EntityState.Detached;
 }
    }
}
