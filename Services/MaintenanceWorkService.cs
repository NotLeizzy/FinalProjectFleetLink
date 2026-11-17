using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record ScheduledVehicleTrip(
    int TripId,
    DateTime PlannedDeparture,
    DateTime PlannedArrival,
    string Origin,
    string Destination);

public sealed record StartWorkResult(
    bool Success,
    string? ErrorMessage = null,
    IReadOnlyList<ScheduledVehicleTrip>? ScheduledTripsWarning = null);

public sealed record RecordServiceResult(bool Success, string? ErrorMessage = null);

public sealed class MaintenanceWorkService
{
    public async Task<StartWorkResult> StartWorkAsync(int maintenanceId)
    {
        EnsureMaintenanceOfficer();
        await using var db = DbContextFactory.Create();
        var item = await db.Maintenances
            .ForCurrentBranch()
            .Include(record => record.Vehicle)
            .SingleOrDefaultAsync(record => record.MaintenanceId == maintenanceId);
        if (item is null)
        {
            return new StartWorkResult(false, "The maintenance record was not found.");
        }
        if (item.Status != (int)MaintenanceStatus.Scheduled)
        {
            return new StartWorkResult(false, "Only Scheduled maintenance records can be started.");
        }
        if (item.Vehicle.Status == (int)VehicleStatus.Inactive)
        {
            return new StartWorkResult(false, "Inactive vehicles cannot enter maintenance.");
        }

        var now = DateTime.Now;
        var activeTrip = await db.Trips
            .ForCurrentBranch()
            .Where(trip => trip.VehicleId == item.VehicleId
                && (trip.Status == (int)TripStatus.Dispatched || trip.Status == (int)TripStatus.InTransit))
            .Select(trip => new { trip.TripId })
            .FirstOrDefaultAsync();
        if (item.Vehicle.Status == (int)VehicleStatus.OnTrip || activeTrip is not null)
        {
            return new StartWorkResult(false, "This vehicle is on a trip and cannot enter maintenance.");
        }

        var scheduledTrips = await db.Trips
            .ForCurrentBranch()
            .Where(trip => trip.VehicleId == item.VehicleId
                && trip.Status == (int)TripStatus.Scheduled
                && trip.PlannedDeparture >= now
                && trip.PlannedDeparture <= now.AddDays(MaintenanceDueSettings.DueSoonDays))
            .OrderBy(trip => trip.PlannedDeparture)
            .Select(trip => new ScheduledVehicleTrip(
                trip.TripId,
                trip.PlannedDeparture,
                trip.PlannedArrival,
                trip.Origin,
                trip.Destination))
            .ToListAsync();

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            item.Status = (int)MaintenanceStatus.InProgress;
            item.Vehicle.Status = (int)VehicleStatus.InMaintenance;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new StartWorkResult(true, ScheduledTripsWarning: scheduledTrips);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<RecordServiceResult> RecordServiceAsync(int maintenanceId, ServiceRecordRequest request)
    {
        EnsureMaintenanceOfficer();
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.CompletedDate == default)
        {
            return new RecordServiceResult(false, "Completed date is required.");
        }
        if (request.CompletedDate > today)
        {
            return new RecordServiceResult(false, "Completed date cannot be in the future.");
        }
        if (request.OdometerAtService < 0)
        {
            return new RecordServiceResult(false, "Odometer at service is required and must be 0 or greater.");
        }
        if (request.Cost.HasValue && request.Cost.Value < 0)
        {
            return new RecordServiceResult(false, "Cost must be 0 or greater.");
        }
        if (request.Notes?.Length > 500)
        {
            return new RecordServiceResult(false, "Notes cannot exceed 500 characters.");
        }

        await using var db = DbContextFactory.Create();
        var item = await db.Maintenances
            .ForCurrentBranch()
            .Include(record => record.Vehicle)
            .SingleOrDefaultAsync(record => record.MaintenanceId == maintenanceId);
        if (item is null)
        {
            return new RecordServiceResult(false, "The maintenance record was not found.");
        }
        if (item.Status != (int)MaintenanceStatus.InProgress)
        {
            return new RecordServiceResult(false, "Only InProgress maintenance records can be completed. Start Work first.");
        }

        var backDated = request.OdometerAtService < item.Vehicle.CurrentOdometer;
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            item.Status = (int)MaintenanceStatus.Completed;
            item.CompletedDate = request.CompletedDate;
            item.OdometerAtService = request.OdometerAtService;
            item.Cost = request.Cost;
            item.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            if (request.OdometerAtService > item.Vehicle.CurrentOdometer)
            {
                item.Vehicle.CurrentOdometer = request.OdometerAtService;
            }
            if (item.Vehicle.Status == (int)VehicleStatus.InMaintenance
                && !await HasOtherInProgressAsync(db, item.VehicleId, item.MaintenanceId))
            {
                item.Vehicle.Status = (int)VehicleStatus.Available;
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new RecordServiceResult(true, backDated ? "The service was recorded with a back-dated odometer." : null);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static Task<bool> HasOtherInProgressAsync(FleetLinkDbContext db, int vehicleId, int excludedId) => db.Maintenances
        .ForCurrentBranch()
        .AnyAsync(record => record.VehicleId == vehicleId
            && record.MaintenanceId != excludedId
            && record.Status == (int)MaintenanceStatus.InProgress);

    private static void EnsureMaintenanceOfficer()
    {
        var user = Session.CurrentUser ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (user.Role != Role.MaintenanceOfficer
            || Permissions.GetAccessLevel(user.Role, Module.Maintenance) != AccessLevel.Full)
        {
            throw new UnauthorizedAccessException("Only Maintenance Officers can start work or record service.");
        }
    }
}
