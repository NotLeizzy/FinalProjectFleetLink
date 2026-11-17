using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record TripShipmentInfo(
    int TripId,
    TripStatus Status,
    string VehiclePlate,
    VehicleType VehicleType,
    decimal CapacityKg,
    decimal TotalWeightKg,
    string OriginBranch);

public sealed record TripShipmentItem(
    int ShipmentId,
    string ReferenceNo,
    string Description,
    decimal WeightKg,
    CargoType CargoType,
    string Origin,
    string Destination,
    ShipmentStatus Status);

public sealed record TripShipmentSummary(
    decimal TotalWeightKg,
    decimal CapacityKg,
    decimal RemainingCapacityKg,
    bool OverCapacity,
    bool CargoTypesCompatible,
    string? CargoRuleMessage);

public sealed record TripShipmentView(
    TripShipmentInfo Trip,
    IReadOnlyList<TripShipmentItem> AssignedShipments,
    IReadOnlyList<TripShipmentItem> AvailableShipments,
    TripShipmentSummary Summary);

public sealed record TripShipmentActionResult(bool Success, string? ErrorMessage = null);

public sealed class TripShipmentService
{
    public async Task<TripShipmentView> GetViewAsync(
        int tripId,
        string? search,
        CargoType? cargoType)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        var trip = await GetScopedTripQuery(db)
            .Include(item => item.Vehicle)
            .Include(item => item.OriginBranch)
            .Include(item => item.Shipments)
            .SingleOrDefaultAsync(item => item.TripId == tripId)
            ?? throw new InvalidOperationException("The selected trip was not found.");

        var assigned = trip.Shipments
            .OrderBy(item => item.ReferenceNo)
            .Select(ToItem)
            .ToList();
        var assignedIds = assigned.Select(item => item.ShipmentId).ToHashSet();
        var availableQuery = db.Shipments
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(item => item.BranchId == trip.OriginBranchId
                && item.Status == (int)ShipmentStatus.Pending
                && !assignedIds.Contains(item.ShipmentId)
                && !item.Trips.Any(otherTrip =>
                    otherTrip.TripId != tripId
                    && ActiveStatuses.Contains((TripStatus)otherTrip.Status)));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            availableQuery = availableQuery.Where(item =>
                item.ReferenceNo.Contains(term)
                || (item.Description != null && item.Description.Contains(term))
                || item.Origin.Contains(term)
                || item.Destination.Contains(term));
        }
        if (cargoType.HasValue)
        {
            availableQuery = availableQuery.Where(item => item.CargoType == (int)cargoType.Value);
        }

        var available = await availableQuery
            .OrderBy(item => item.ReferenceNo)
            .Select(item => new TripShipmentItem(
                item.ShipmentId,
                item.ReferenceNo,
                item.Description ?? string.Empty,
                item.WeightKg,
                (CargoType)item.CargoType,
                item.Origin,
                item.Destination,
                (ShipmentStatus)item.Status))
            .ToListAsync();
        var summary = BuildSummary(trip, assigned);
        return new TripShipmentView(
            new TripShipmentInfo(
                trip.TripId,
                (TripStatus)trip.Status,
                trip.Vehicle.PlateNo,
                (VehicleType)trip.Vehicle.VehicleType,
                trip.Vehicle.CapacityKg,
                summary.TotalWeightKg,
                trip.OriginBranch.BranchName),
            assigned,
            available,
            summary);
    }

    public async Task<TripShipmentActionResult> AddAsync(int tripId, int shipmentId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        var trip = await GetScopedTripQuery(db)
            .Include(item => item.Vehicle)
            .Include(item => item.Shipments)
            .SingleOrDefaultAsync(item => item.TripId == tripId);
        if (trip is null)
        {
            return Failure("The trip was not found.");
        }
        if (trip.Status != (int)TripStatus.Scheduled)
        {
            return Failure("Shipments can only be managed while the trip is Scheduled.");
        }
        if (trip.Shipments.Any(item => item.ShipmentId == shipmentId))
        {
            return Failure("The shipment is already on this trip.");
        }

        var shipment = await db.Shipments
            .ForCurrentBranch()
            .SingleOrDefaultAsync(item => item.ShipmentId == shipmentId);
        if (shipment is null || shipment.BranchId != trip.OriginBranchId)
        {
            return Failure("The shipment was not found in the trip's origin branch.");
        }
        if (shipment.Status != (int)ShipmentStatus.Pending)
        {
            return Failure("Only Pending shipments can be added.");
        }
        if (await db.Trips
            .ForCurrentBranch()
            .AnyAsync(otherTrip =>
                otherTrip.TripId != tripId
                && ActiveStatuses.Contains((TripStatus)otherTrip.Status)
                && otherTrip.Shipments.Any(item => item.ShipmentId == shipmentId)))
        {
            return Failure("The shipment is already assigned to another active trip.");
        }

        var newTotal = trip.Shipments.Sum(item => item.WeightKg) + shipment.WeightKg;
        if (newTotal > trip.Vehicle.CapacityKg)
        {
            return Failure($"Rule C5 blocked the add: total weight would be {newTotal:0.##} kg, exceeding vehicle capacity of {trip.Vehicle.CapacityKg:0.##} kg.");
        }
        var cargoMessage = GetCargoRuleMessage(trip.Vehicle.VehicleType, trip.Shipments.Select(item => (CargoType)item.CargoType).Append((CargoType)shipment.CargoType));
        if (cargoMessage is not null)
        {
            return Failure($"Rule C6 blocked the add: {cargoMessage}");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        shipment.Status = (int)ShipmentStatus.Assigned;
        trip.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new TripShipmentActionResult(true);
    }

    public async Task<TripShipmentActionResult> RemoveAsync(int tripId, int shipmentId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        var trip = await GetScopedTripQuery(db)
            .Include(item => item.Shipments)
            .SingleOrDefaultAsync(item => item.TripId == tripId);
        if (trip is null)
        {
            return Failure("The trip was not found.");
        }
        if (trip.Status != (int)TripStatus.Scheduled)
        {
            return Failure("Shipments can only be managed while the trip is Scheduled.");
        }
        var shipment = trip.Shipments.SingleOrDefault(item => item.ShipmentId == shipmentId);
        if (shipment is null)
        {
            return Failure("The shipment is not on this trip.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        trip.Shipments.Remove(shipment);
        shipment.Status = (int)ShipmentStatus.Pending;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new TripShipmentActionResult(true);
    }

    private static IQueryable<Trip> GetScopedTripQuery(FleetLinkDbContext db)
    {
        return db.Trips.ForCurrentBranch();
    }

    private static TripShipmentSummary BuildSummary(Trip trip, IReadOnlyList<TripShipmentItem> shipments)
    {
        var total = shipments.Sum(item => item.WeightKg);
        var message = GetCargoRuleMessage(trip.Vehicle.VehicleType, shipments.Select(item => item.CargoType));
        return new TripShipmentSummary(
            total,
            trip.Vehicle.CapacityKg,
            trip.Vehicle.CapacityKg - total,
            total > trip.Vehicle.CapacityKg,
            message is null,
            message);
    }

    private static string? GetCargoRuleMessage(int vehicleType, IEnumerable<CargoType> cargoTypes)
    {
        var type = (VehicleType)vehicleType;
        if (cargoTypes.Contains(CargoType.ColdChain) && type != VehicleType.Refrigerated)
        {
            return "Cold Chain cargo requires a Refrigerated vehicle.";
        }
        if (cargoTypes.Contains(CargoType.Oversized)
            && type is not VehicleType.WingVan
            and not VehicleType.TrailerTruck)
        {
            return "Oversized cargo requires a Wing Van or Trailer Truck.";
        }
        return null;
    }

    private static TripShipmentItem ToItem(Shipment shipment) => new(
        shipment.ShipmentId,
        shipment.ReferenceNo,
        shipment.Description ?? string.Empty,
        shipment.WeightKg,
        (CargoType)shipment.CargoType,
        shipment.Origin,
        shipment.Destination,
        (ShipmentStatus)shipment.Status);

    private static TripShipmentActionResult Failure(string message) => new(false, message);

    private static void EnsureDispatcherAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Trips) != AccessLevel.Full)
        {
            throw new UnauthorizedAccessException("Only Dispatchers can manage trip shipments.");
        }
    }

    private static readonly TripStatus[] ActiveStatuses =
    [
        TripStatus.Scheduled,
        TripStatus.Dispatched,
        TripStatus.InTransit
    ];
}
