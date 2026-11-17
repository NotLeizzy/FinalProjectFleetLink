using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record CheckResult(string Code, string Name, bool Passed, string Message);

public sealed record DispatchOperationResult(
    bool Success,
    IReadOnlyList<CheckResult> Checks,
    string? ErrorMessage = null);

public sealed class DispatchService
{
    private static readonly TripStatus[] ActiveTripStatuses =
    [
        TripStatus.Scheduled,
        TripStatus.Dispatched,
        TripStatus.InTransit
    ];

    public async Task<IReadOnlyList<CheckResult>> RunChecksAsync(int tripId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        return await RunChecksAsync(db, tripId);
    }

    public async Task<DispatchOperationResult> DispatchAsync(int tripId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var checks = await RunChecksAsync(db, tripId);
            if (checks.Any(check => !check.Passed))
            {
                await transaction.RollbackAsync();
                return new DispatchOperationResult(false, checks, "Dispatch checks failed.");
            }

            var trip = await GetScopedTripQuery(db)
                .Include(item => item.Vehicle)
                .Include(item => item.Driver)
                .SingleOrDefaultAsync(item => item.TripId == tripId);
            if (trip is null)
            {
                await transaction.RollbackAsync();
                return new DispatchOperationResult(false, checks, "The trip was not found.");
            }

            var passedSummary = string.Join("; ", checks.Select(check => $"{check.Code} {check.Name}: passed"));
            db.Dispatches.Add(new Dispatch
            {
                TripId = trip.TripId,
                DispatchedByUserId = Session.CurrentUser!.UserId,
                DispatchedAt = DateTime.Now,
                CheckSummary = passedSummary.Length > 500 ? passedSummary[..500] : passedSummary
            });
            trip.Status = (int)TripStatus.Dispatched;
            trip.Vehicle.Status = (int)VehicleStatus.OnTrip;
            trip.Driver.Status = (int)DriverStatus.OnTrip;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new DispatchOperationResult(true, checks);
        }
        catch (DbUpdateException exception) when (IsDispatchUniqueViolation(exception))
        {
            await transaction.RollbackAsync();
            return new DispatchOperationResult(false, [], "This trip was already dispatched");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<IReadOnlyList<CheckResult>> RunChecksAsync(FleetLinkDbContext db, int tripId)
    {
        var trip = await GetScopedTripQuery(db)
            .Include(item => item.Vehicle)
            .Include(item => item.Driver)
            .Include(item => item.Shipments)
            .SingleOrDefaultAsync(item => item.TripId == tripId);
        if (trip is null)
        {
            return [new CheckResult("TRIP", "Trip", false, "The trip was not found or is outside your branch scope.")];
        }

        var checks = new List<CheckResult>
        {
            new("C0", "Trip status", trip.Status == (int)TripStatus.Scheduled,
                trip.Status == (int)TripStatus.Scheduled
                    ? "Trip is Scheduled."
                    : $"Trip status is {(TripStatus)trip.Status}; only Scheduled trips can be dispatched."),
            await CheckVehicleAvailabilityAsync(db, trip),
            await CheckDriverAvailabilityAsync(db, trip),
            new("C3", "Registration", trip.Vehicle.RegistrationExpiry >= DateOnly.FromDateTime(trip.PlannedArrival),
                trip.Vehicle.RegistrationExpiry >= DateOnly.FromDateTime(trip.PlannedArrival)
                    ? $"Registration expires {trip.Vehicle.RegistrationExpiry:yyyy-MM-dd}."
                    : $"Registration expires {trip.Vehicle.RegistrationExpiry:yyyy-MM-dd}, before the planned arrival {trip.PlannedArrival:yyyy-MM-dd}."),
            new("C4", "License", trip.Driver.LicenseExpiry >= DateOnly.FromDateTime(trip.PlannedArrival),
                trip.Driver.LicenseExpiry >= DateOnly.FromDateTime(trip.PlannedArrival)
                    ? $"License expires {trip.Driver.LicenseExpiry:yyyy-MM-dd}."
                    : $"License expires {trip.Driver.LicenseExpiry:yyyy-MM-dd}, before the planned arrival {trip.PlannedArrival:yyyy-MM-dd}."),
            BuildCapacityCheck(trip),
            BuildCargoCheck(trip),
            await CheckMaintenanceAsync(db, trip),
            new("C8", "At least one shipment", trip.Shipments.Count > 0,
                trip.Shipments.Count > 0 ? $"Trip has {trip.Shipments.Count} shipment(s)." : "The trip must have at least one shipment.")
        };
        return checks;
    }

    private static async Task<CheckResult> CheckVehicleAvailabilityAsync(FleetLinkDbContext db, Trip trip)
    {
        var hasOverlap = await db.Trips
            .ForCurrentBranch()
            .AnyAsync(other => other.TripId != trip.TripId
                && other.VehicleId == trip.VehicleId
                && ActiveTripStatuses.Contains((TripStatus)other.Status)
                && other.PlannedDeparture < trip.PlannedArrival
                && trip.PlannedDeparture < other.PlannedArrival);
        var passed = trip.Vehicle.Status == (int)VehicleStatus.Available && !hasOverlap;
        var message = trip.Vehicle.Status != (int)VehicleStatus.Available
            ? $"Vehicle status is {(VehicleStatus)trip.Vehicle.Status}, not Available."
            : hasOverlap ? "The vehicle has another active trip with an overlapping planned window." : "Vehicle is Available and has no overlapping active trip.";
        return new CheckResult("C1", "Vehicle availability", passed, message);
    }

    private static async Task<CheckResult> CheckDriverAvailabilityAsync(FleetLinkDbContext db, Trip trip)
    {
        var hasOverlap = await db.Trips
            .ForCurrentBranch()
            .AnyAsync(other => other.TripId != trip.TripId
                && other.DriverId == trip.DriverId
                && ActiveTripStatuses.Contains((TripStatus)other.Status)
                && other.PlannedDeparture < trip.PlannedArrival
                && trip.PlannedDeparture < other.PlannedArrival);
        var passed = trip.Driver.Status == (int)DriverStatus.Active && !hasOverlap;
        var message = trip.Driver.Status != (int)DriverStatus.Active
            ? $"Driver status is {(DriverStatus)trip.Driver.Status}, not Active."
            : hasOverlap ? "The driver has another active trip with an overlapping planned window." : "Driver is Active and has no overlapping active trip.";
        return new CheckResult("C2", "Driver availability", passed, message);
    }

    private static CheckResult BuildCapacityCheck(Trip trip)
    {
        var total = trip.Shipments.Sum(item => item.WeightKg);
        var passed = total <= trip.Vehicle.CapacityKg;
        return new CheckResult("C5", "Capacity", passed,
            passed
                ? $"{total:0.##} kg on the trip, vehicle capacity is {trip.Vehicle.CapacityKg:0.##} kg."
                : $"{total:0.##} kg on the trip, vehicle capacity is {trip.Vehicle.CapacityKg:0.##} kg.");
    }

    private static CheckResult BuildCargoCheck(Trip trip)
    {
        var cargoTypes = trip.Shipments.Select(item => (CargoType)item.CargoType).ToList();
        var message = GetCargoRuleMessage((VehicleType)trip.Vehicle.VehicleType, cargoTypes);
        return new CheckResult("C6", "Cargo type", message is null, message ?? "All cargo types fit the vehicle type.");
    }

    private static async Task<CheckResult> CheckMaintenanceAsync(FleetLinkDbContext db, Trip trip)
    {
        var maintenance = await db.Maintenances
            .ForCurrentBranch()
            .Include(item => item.Vehicle)
            .Where(item => item.VehicleId == trip.VehicleId)
            .ToListAsync();
        var hasBlocking = maintenance.Any(item =>
            MaintenanceDueHelper.IsBlocking(item, trip.Vehicle, trip.PlannedDeparture));
        return new CheckResult("C7", "Maintenance", !hasBlocking,
            hasBlocking ? "The vehicle has scheduled or in-progress maintenance due before departure or by odometer." : "No blocking maintenance is due.");
    }

    private static string? GetCargoRuleMessage(VehicleType vehicleType, IReadOnlyCollection<CargoType> cargoTypes)
    {
        if (cargoTypes.Contains(CargoType.ColdChain) && vehicleType != VehicleType.Refrigerated)
        {
            return "Cold Chain cargo requires a Refrigerated vehicle.";
        }
        if (cargoTypes.Contains(CargoType.Oversized)
            && vehicleType is not VehicleType.WingVan
            and not VehicleType.TrailerTruck)
        {
            return "Oversized cargo requires a Wing Van or Trailer Truck.";
        }
        return null;
    }

    private static IQueryable<Trip> GetScopedTripQuery(FleetLinkDbContext db) => db.Trips.ForCurrentBranch();

    private static bool IsDispatchUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && (sqlException.Number is 2601 or 2627)
            && sqlException.Message.Contains("Dispatch", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureDispatcherAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Dispatch) != AccessLevel.Full)
        {
            throw new UnauthorizedAccessException("Only Dispatchers can dispatch trips.");
        }
    }
}
