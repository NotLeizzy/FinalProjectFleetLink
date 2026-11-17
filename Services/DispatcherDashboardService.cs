using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record DispatcherDashboardKpis(
    int PendingShipments,
    int TripsScheduledToday,
    int TripsInTransit,
    int CompletedToday,
    int VehiclesAvailable,
    int DriversActive);

public sealed record DispatchQueueItem(
    int TripId,
    string VehiclePlate,
    string DriverName,
    DateTime PlannedDeparture,
    DateTime PlannedArrival,
    int ShipmentCount,
    string BranchName);

public sealed record LateTripItem(
    int TripId,
    string VehiclePlate,
    string DriverName,
    DateTime PlannedArrival,
    int ShipmentCount,
    string BranchName);

public sealed record DispatcherDashboardSnapshot(
    DispatcherDashboardKpis Kpis,
    IReadOnlyList<DispatchQueueItem> TripsNeedingDispatch,
    IReadOnlyList<LateTripItem> LateTrips);

public sealed class DispatcherDashboardService
{
    public async Task<DispatcherDashboardSnapshot> GetSnapshotAsync(int? branchId)
    {
        EnsureDispatcherAccess();
        ValidateBranch(branchId);

        await using var db = DbContextFactory.Create();
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var next24Hours = now.AddHours(24);
        var shipments = db.Shipments.AsNoTracking().ForCurrentBranch();
        var trips = db.Trips.AsNoTracking().ForCurrentBranch();
        var vehicles = db.Vehicles.AsNoTracking().ForCurrentBranch();
        var drivers = db.Drivers.AsNoTracking().ForCurrentBranch();

        if (branchId.HasValue)
        {
            shipments = shipments.Where(item => item.BranchId == branchId.Value);
            trips = trips.Where(item => item.OriginBranchId == branchId.Value);
            vehicles = vehicles.Where(item => item.BranchId == branchId.Value);
            drivers = drivers.Where(item => item.BranchId == branchId.Value);
        }

        var kpis = new DispatcherDashboardKpis(
            await shipments.CountAsync(item => item.Status == (int)ShipmentStatus.Pending),
            await trips.CountAsync(item => item.Status == (int)TripStatus.Scheduled
                && item.PlannedDeparture >= today
                && item.PlannedDeparture < tomorrow),
            await trips.CountAsync(item => item.Status == (int)TripStatus.InTransit),
            await trips.CountAsync(item => item.Status == (int)TripStatus.Completed
                && item.ActualArrival >= today
                && item.ActualArrival < tomorrow),
            await vehicles.CountAsync(item => item.Status == (int)VehicleStatus.Available),
            await drivers.CountAsync(item => item.Status == (int)DriverStatus.Active));

        var tripsNeedingDispatch = await trips
            .Where(item => item.Status == (int)TripStatus.Scheduled
                && item.PlannedDeparture >= now
                && item.PlannedDeparture <= next24Hours)
            .OrderBy(item => item.PlannedDeparture)
            .Select(item => new DispatchQueueItem(
                item.TripId,
                item.Vehicle.PlateNo,
                item.Driver.FullName,
                item.PlannedDeparture,
                item.PlannedArrival,
                item.Shipments.Count,
                item.OriginBranch.BranchName))
            .ToListAsync();

        var lateTrips = await trips
            .Where(item => item.Status == (int)TripStatus.InTransit
                && item.PlannedArrival < now)
            .OrderBy(item => item.PlannedArrival)
            .Select(item => new LateTripItem(
                item.TripId,
                item.Vehicle.PlateNo,
                item.Driver.FullName,
                item.PlannedArrival,
                item.Shipments.Count,
                item.OriginBranch.BranchName))
            .ToListAsync();

        return new DispatcherDashboardSnapshot(kpis, tripsNeedingDispatch, lateTrips);
    }

    private static void ValidateBranch(int? branchId)
    {
        if (!Session.IsHeadOffice && branchId != Session.CurrentUser!.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }
    }

    private static void EnsureDispatcherAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Dashboard) != AccessLevel.Full
            || user.Role != Role.Dispatcher)
        {
            throw new UnauthorizedAccessException("Only Dispatchers can view the Dispatcher dashboard.");
        }
    }
}
