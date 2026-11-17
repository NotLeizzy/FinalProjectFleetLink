using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record TripListItem(
    int TripId,
    string VehiclePlate,
    string DriverName,
    string Origin,
    string Destination,
    DateTime PlannedDeparture,
    DateTime PlannedArrival,
    int ShipmentCount,
    decimal TotalWeightKg,
    decimal VehicleCapacityKg,
    TripStatus Status,
    string OriginBranch);

public sealed record TripRequest(
    int OriginBranchId,
    int VehicleId,
    int DriverId,
    string Origin,
    string Destination,
    int? DestinationBranchId,
    DateTime PlannedDeparture,
    DateTime PlannedArrival,
    decimal? DistanceKm);

public sealed record TripActionResult(
    bool Success,
    string? ErrorMessage = null,
    bool Warning = false);

public sealed class TripService
{
    private static readonly TripStatus[] ActiveStatuses =
    [
        TripStatus.Scheduled,
        TripStatus.Dispatched,
        TripStatus.InTransit
    ];

    public async Task<IReadOnlyList<TripListItem>> GetTripsAsync(
        TripStatus? status,
        DateTime? plannedFrom,
        DateTime? plannedTo,
        int? vehicleId,
        int? branchId,
        bool showCompletedCancelled)
    {
        EnsureDispatcherAccess();
        ValidateBranch(branchId);
        if (plannedFrom.HasValue && plannedTo.HasValue && plannedFrom.Value.Date > plannedTo.Value.Date)
        {
            throw new ArgumentException("The planned departure date range is invalid.");
        }

        await using var db = DbContextFactory.Create();
        var query = db.Trips
            .AsNoTracking()
            .ForCurrentBranch();
        if (status.HasValue)
        {
            query = query.Where(trip => trip.Status == (int)status.Value);
        }
        else if (!showCompletedCancelled)
        {
            query = query.Where(trip =>
                trip.Status != (int)TripStatus.Completed
                && trip.Status != (int)TripStatus.Cancelled);
        }
        if (plannedFrom.HasValue)
        {
            query = query.Where(trip => trip.PlannedDeparture >= plannedFrom.Value.Date);
        }
        if (plannedTo.HasValue)
        {
            query = query.Where(trip => trip.PlannedDeparture < plannedTo.Value.Date.AddDays(1));
        }
        if (vehicleId.HasValue)
        {
            query = query.Where(trip => trip.VehicleId == vehicleId.Value);
        }
        if (branchId.HasValue)
        {
            query = query.Where(trip => trip.OriginBranchId == branchId.Value);
        }

        return await query
            .OrderByDescending(trip => trip.PlannedDeparture)
            .Select(trip => new TripListItem(
                trip.TripId,
                trip.Vehicle.PlateNo,
                trip.Driver.FullName,
                trip.Origin,
                trip.Destination,
                trip.PlannedDeparture,
                trip.PlannedArrival,
                trip.Shipments.Count,
                trip.Shipments.Sum(shipment => (decimal?)shipment.WeightKg) ?? 0,
                trip.Vehicle.CapacityKg,
                (TripStatus)trip.Status,
                trip.OriginBranch.BranchName))
            .ToListAsync();
    }

    public async Task<TripListItem?> GetByIdAsync(int tripId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        return await db.Trips
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(trip => trip.TripId == tripId)
            .Select(trip => new TripListItem(
                trip.TripId,
                trip.Vehicle.PlateNo,
                trip.Driver.FullName,
                trip.Origin,
                trip.Destination,
                trip.PlannedDeparture,
                trip.PlannedArrival,
                trip.Shipments.Count,
                trip.Shipments.Sum(shipment => (decimal?)shipment.WeightKg) ?? 0,
                trip.Vehicle.CapacityKg,
                (TripStatus)trip.Status,
                trip.OriginBranch.BranchName))
            .SingleOrDefaultAsync();
    }

    public async Task<TripActionResult> CreateAsync(TripRequest request)
    {
        EnsureDispatcherAccess();
        ValidateRequest(request);
        await using var db = DbContextFactory.Create();
        var validation = await ValidateResourcesAsync(db, request, null);
        if (!validation.Success)
        {
            return validation;
        }

        db.Trips.Add(new Trip
        {
            OriginBranchId = request.OriginBranchId,
            VehicleId = request.VehicleId,
            DriverId = request.DriverId,
            Origin = request.Origin.Trim(),
            Destination = request.Destination.Trim(),
            DestinationBranchId = request.DestinationBranchId,
            PlannedDeparture = request.PlannedDeparture,
            PlannedArrival = request.PlannedArrival,
            DistanceKm = request.DistanceKm,
            Status = (int)TripStatus.Scheduled,
            CreatedByUserId = Session.CurrentUser!.UserId
        });
        await db.SaveChangesAsync();
        return new TripActionResult(true, Warning: validation.Warning);
    }

    public async Task<TripActionResult> UpdateAsync(int tripId, TripRequest request)
    {
        EnsureDispatcherAccess();
        ValidateRequest(request);
        await using var db = DbContextFactory.Create();
        var trip = await db.Trips
            .ForCurrentBranch()
            .SingleOrDefaultAsync(item => item.TripId == tripId);
        if (trip is null)
        {
            return Failure("The trip was not found.");
        }
        if (trip.Status != (int)TripStatus.Scheduled)
        {
            return Failure("Only Scheduled trips can be edited.");
        }

        var validation = await ValidateResourcesAsync(db, request, tripId);
        if (!validation.Success)
        {
            return validation;
        }
        trip.OriginBranchId = request.OriginBranchId;
        trip.VehicleId = request.VehicleId;
        trip.DriverId = request.DriverId;
        trip.Origin = request.Origin.Trim();
        trip.Destination = request.Destination.Trim();
        trip.DestinationBranchId = request.DestinationBranchId;
        trip.PlannedDeparture = request.PlannedDeparture;
        trip.PlannedArrival = request.PlannedArrival;
        trip.DistanceKm = request.DistanceKm;
        await db.SaveChangesAsync();
        return new TripActionResult(true, Warning: validation.Warning);
    }

    public async Task<TripActionResult> CancelAsync(int tripId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        var trip = await db.Trips
            .ForCurrentBranch()
            .Include(item => item.Shipments)
            .Include(item => item.Vehicle)
            .Include(item => item.Driver)
            .SingleOrDefaultAsync(item => item.TripId == tripId);
        if (trip is null)
        {
            return Failure("The trip was not found.");
        }
        if (trip.Status != (int)TripStatus.Scheduled && trip.Status != (int)TripStatus.Dispatched)
        {
            return Failure("Only Scheduled or Dispatched trips can be cancelled.");
        }

        var wasDispatched = trip.Status == (int)TripStatus.Dispatched;
        await using var transaction = await db.Database.BeginTransactionAsync();
        trip.Status = (int)TripStatus.Cancelled;
        foreach (var shipment in trip.Shipments)
        {
            shipment.Status = (int)ShipmentStatus.Pending;
        }
        trip.Shipments.Clear();
        if (wasDispatched)
        {
            trip.Vehicle.Status = (int)VehicleStatus.Available;
            trip.Driver.Status = (int)DriverStatus.Active;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new TripActionResult(true);
    }

    private static async Task<TripActionResult> ValidateResourcesAsync(
        FleetLinkDbContext db,
        TripRequest request,
        int? excludedTripId)
    {
        var vehicle = await db.Vehicles
            .ForCurrentBranch()
            .SingleOrDefaultAsync(item => item.VehicleId == request.VehicleId);
        var driver = await db.Drivers
            .ForCurrentBranch()
            .SingleOrDefaultAsync(item => item.DriverId == request.DriverId);
        if (vehicle is null || driver is null)
        {
            return Failure("The selected vehicle or driver was not found or is outside your branch scope.");
        }
        if (vehicle.BranchId != request.OriginBranchId || driver.BranchId != request.OriginBranchId)
        {
            return Failure("The vehicle and driver must both belong to the selected Origin Branch.");
        }
        if (vehicle.Status == (int)VehicleStatus.Inactive || driver.Status == (int)DriverStatus.Inactive)
        {
            return Failure("Inactive vehicles and drivers cannot be scheduled.");
        }

        var conflict = await db.Trips
            .ForCurrentBranch()
            .Where(trip => ActiveStatuses.Contains((TripStatus)trip.Status)
                && (!excludedTripId.HasValue || trip.TripId != excludedTripId.Value)
                && (trip.VehicleId == request.VehicleId || trip.DriverId == request.DriverId)
                && trip.PlannedDeparture < request.PlannedArrival
                && request.PlannedDeparture < trip.PlannedArrival)
            .Select(trip => new
            {
                trip.TripId,
                trip.VehicleId,
                trip.DriverId,
                trip.PlannedDeparture,
                trip.PlannedArrival
            })
            .FirstOrDefaultAsync();
        if (conflict is not null)
        {
            var resource = conflict.VehicleId == request.VehicleId ? "vehicle" : "driver";
            return Failure($"The {resource} conflicts with Trip {conflict.TripId} from {conflict.PlannedDeparture:g} to {conflict.PlannedArrival:g}.");
        }

        var warning = vehicle.Status != (int)VehicleStatus.Available;
        return new TripActionResult(true, Warning: warning);
    }

    private static void ValidateRequest(TripRequest request)
    {
        var now = DateTime.Now;
        if (request.PlannedArrival <= request.PlannedDeparture)
        {
            throw new ArgumentException("Planned Arrival must be after Planned Departure.");
        }
        if (request.PlannedDeparture < now.AddMinutes(-5))
        {
            throw new ArgumentException("Planned Departure cannot be more than five minutes in the past.");
        }
        if (request.OriginBranchId <= 0)
        {
            throw new ArgumentException("Origin Branch is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Origin) || request.Origin.Trim().Length > 150)
        {
            throw new ArgumentException("Origin is required and must be at most 150 characters.");
        }
        if (string.IsNullOrWhiteSpace(request.Destination) || request.Destination.Trim().Length > 150)
        {
            throw new ArgumentException("Destination is required and must be at most 150 characters.");
        }
        if (request.DistanceKm.HasValue && request.DistanceKm.Value < 0)
        {
            throw new ArgumentException("Distance cannot be negative.");
        }
    }

    private static void ValidateBranch(int? branchId)
    {
        if (!Session.IsHeadOffice && branchId != Session.CurrentUser!.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }
    }

    private static TripActionResult Failure(string message) => new(false, message);

    private static void EnsureDispatcherAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Trips) != AccessLevel.Full)
        {
            throw new UnauthorizedAccessException("Only Dispatchers can manage trips.");
        }
    }
}
