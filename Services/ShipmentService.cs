using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record ShipmentListItem(
    int ShipmentId,
    int BranchId,
    string ReferenceNo,
    string Description,
    decimal WeightKg,
    CargoType CargoType,
    string Origin,
    string Destination,
    ShipmentStatus Status,
    int? TripId,
    string BranchName,
    DateTime CreatedAt);

public sealed record ShipmentRequest(
    int BranchId,
    string ReferenceNo,
    string? Description,
    decimal WeightKg,
    CargoType CargoType,
    string Origin,
    string Destination);

public sealed record ShipmentActionResult(bool Success, string? ErrorMessage = null);

public sealed class ShipmentService
{
    private readonly ReferenceNumberGenerator referenceNumberGenerator = new();

    public async Task<IReadOnlyList<ShipmentListItem>> GetShipmentsAsync(
        string? search,
        ShipmentStatus? status,
        CargoType? cargoType,
        int? branchId,
        DateTime? createdFrom,
        DateTime? createdTo)
    {
        EnsureDispatcherAccess();
        ValidateBranch(branchId);
        ValidateDateRange(createdFrom, createdTo);

        await using var db = DbContextFactory.Create();
        var query = db.Shipments
            .AsNoTracking()
            .ForCurrentBranch();
        if (branchId.HasValue)
        {
            query = query.Where(shipment => shipment.BranchId == branchId.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(shipment =>
                shipment.ReferenceNo.Contains(term)
                || (shipment.Description != null && shipment.Description.Contains(term))
                || shipment.Origin.Contains(term)
                || shipment.Destination.Contains(term));
        }
        if (status.HasValue)
        {
            query = query.Where(shipment => shipment.Status == (int)status.Value);
        }
        if (cargoType.HasValue)
        {
            query = query.Where(shipment => shipment.CargoType == (int)cargoType.Value);
        }
        if (createdFrom.HasValue)
        {
            query = query.Where(shipment => shipment.CreatedAt >= createdFrom.Value.Date);
        }
        if (createdTo.HasValue)
        {
            query = query.Where(shipment => shipment.CreatedAt < createdTo.Value.Date.AddDays(1));
        }

        return await query
            .OrderByDescending(shipment => shipment.CreatedAt)
            .Select(shipment => new ShipmentListItem(
                shipment.ShipmentId,
                shipment.BranchId,
                shipment.ReferenceNo,
                shipment.Description ?? string.Empty,
                shipment.WeightKg,
                (CargoType)shipment.CargoType,
                shipment.Origin,
                shipment.Destination,
                (ShipmentStatus)shipment.Status,
                shipment.Trips
                    .Select(trip => (int?)trip.TripId)
                    .FirstOrDefault(),
                shipment.Branch.BranchName,
                shipment.CreatedAt))
            .ToListAsync();
    }

    public async Task<ShipmentListItem?> GetByIdAsync(int shipmentId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        return await db.Shipments
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(shipment => shipment.ShipmentId == shipmentId)
            .Select(shipment => new ShipmentListItem(
                shipment.ShipmentId,
                shipment.BranchId,
                shipment.ReferenceNo,
                shipment.Description ?? string.Empty,
                shipment.WeightKg,
                (CargoType)shipment.CargoType,
                shipment.Origin,
                shipment.Destination,
                (ShipmentStatus)shipment.Status,
                shipment.Trips.Select(trip => (int?)trip.TripId).FirstOrDefault(),
                shipment.Branch.BranchName,
                shipment.CreatedAt))
            .SingleOrDefaultAsync();
    }

    public async Task<string> GetSuggestedReferenceAsync()
    {
        EnsureDispatcherAccess();
        return await referenceNumberGenerator.GenerateAsync();
    }

    public async Task<ShipmentActionResult> CreateAsync(ShipmentRequest request)
    {
        EnsureDispatcherAccess();
        var normalized = ValidateRequest(request);
        ValidateBranch(normalized.BranchId);

        await using var db = DbContextFactory.Create();
        if (await ReferenceExistsAsync(db, normalized.ReferenceNo, null))
        {
            return Failure("Reference No already exists.");
        }

        db.Shipments.Add(new Shipment
        {
            BranchId = normalized.BranchId,
            ReferenceNo = normalized.ReferenceNo,
            Description = normalized.Description,
            WeightKg = normalized.WeightKg,
            CargoType = (int)normalized.CargoType,
            Origin = normalized.Origin,
            Destination = normalized.Destination,
            Status = (int)ShipmentStatus.Pending,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        return new ShipmentActionResult(true);
    }

    public async Task<ShipmentActionResult> UpdateAsync(int shipmentId, ShipmentRequest request)
    {
        EnsureDispatcherAccess();
        var normalized = ValidateRequest(request);
        ValidateBranch(normalized.BranchId);

        await using var db = DbContextFactory.Create();
        var shipment = await db.Shipments
            .ForCurrentBranch()
            .SingleOrDefaultAsync(item => item.ShipmentId == shipmentId);
        if (shipment is null)
        {
            return Failure("The shipment was not found.");
        }
        if (shipment.Status != (int)ShipmentStatus.Pending)
        {
            return Failure("Only Pending shipments can be edited.");
        }
        if (await ReferenceExistsAsync(db, normalized.ReferenceNo, shipmentId))
        {
            return Failure("Reference No already exists.");
        }

        shipment.BranchId = normalized.BranchId;
        shipment.ReferenceNo = normalized.ReferenceNo;
        shipment.Description = normalized.Description;
        shipment.WeightKg = normalized.WeightKg;
        shipment.CargoType = (int)normalized.CargoType;
        shipment.Origin = normalized.Origin;
        shipment.Destination = normalized.Destination;
        await db.SaveChangesAsync();
        return new ShipmentActionResult(true);
    }

    public async Task<ShipmentActionResult> CancelAsync(int shipmentId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        var shipment = await db.Shipments
            .ForCurrentBranch()
            .SingleOrDefaultAsync(item => item.ShipmentId == shipmentId);
        if (shipment is null)
        {
            return Failure("The shipment was not found.");
        }
        if (shipment.Status != (int)ShipmentStatus.Pending)
        {
            return Failure("Only Pending shipments can be cancelled.");
        }

        shipment.Status = (int)ShipmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return new ShipmentActionResult(true);
    }

    private static ShipmentRequest ValidateRequest(ShipmentRequest request)
    {
        var referenceNo = request.ReferenceNo.Trim().ToUpperInvariant();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        var origin = request.Origin.Trim();
        var destination = request.Destination.Trim();

        if (referenceNo.Length == 0 || referenceNo.Length > 30)
        {
            throw new ArgumentException("Reference No is required and must be at most 30 characters.");
        }
        if (description is not null && description.Length > 200)
        {
            throw new ArgumentException("Description must be at most 200 characters.");
        }
        if (request.WeightKg <= 0)
        {
            throw new ArgumentException("Weight must be greater than zero.");
        }
        if (origin.Length == 0 || origin.Length > 150)
        {
            throw new ArgumentException("Origin is required and must be at most 150 characters.");
        }
        if (destination.Length == 0 || destination.Length > 150)
        {
            throw new ArgumentException("Destination is required and must be at most 150 characters.");
        }
        if (!Enum.IsDefined(request.CargoType))
        {
            throw new ArgumentException("Cargo Type is invalid.");
        }

        return new ShipmentRequest(
            request.BranchId,
            referenceNo,
            description,
            request.WeightKg,
            request.CargoType,
            origin,
            destination);
    }

    private static async Task<bool> ReferenceExistsAsync(
        FleetLinkDbContext db,
        string referenceNo,
        int? excludedShipmentId)
    {
        var normalized = referenceNo.ToUpper();
        return await db.Shipments.AnyAsync(shipment =>
            shipment.ReferenceNo.ToUpper() == normalized
            && (!excludedShipmentId.HasValue || shipment.ShipmentId != excludedShipmentId.Value));
    }

    private static void ValidateBranch(int? branchId)
    {
        if (!Session.IsHeadOffice && branchId != Session.CurrentUser!.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }
    }

    private static void ValidateDateRange(DateTime? createdFrom, DateTime? createdTo)
    {
        if (createdFrom.HasValue && createdTo.HasValue && createdFrom.Value.Date > createdTo.Value.Date)
        {
            throw new ArgumentException("The Created date range is invalid.");
        }
    }

    private static ShipmentActionResult Failure(string message) => new(false, message);

    private static void EnsureDispatcherAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Shipments) != AccessLevel.Full)
        {
            throw new UnauthorizedAccessException("Only Dispatchers can manage shipments.");
        }
    }
}
