using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record DashboardKpis(
    int TotalVehicles,
    int AvailableVehicles,
    int VehiclesOnTrip,
    int VehiclesInMaintenance,
    int ActiveDrivers,
    int DriversOnLeave,
    int VehiclesWithoutCurrentDriver,
    int DriversWithoutCurrentVehicle,
    int TripsToday,
    int TripsInTransit,
    int AbnormalFuelFlags,
    int MaintenanceDue,
    int RegistrationsExpiring,
    int LicensesExpiring);

public sealed record VehicleStatusCount(string Status, int Count);

public sealed record ExpiringItem(
    string RecordType,
    string Name,
    string Branch,
    DateOnly ExpiryDate);

public sealed record AbnormalFuelItem(
    DateTime FuelDate,
    string PlateNo,
    string Branch,
    decimal Liters,
    decimal TotalCost,
    int Odometer);

public sealed record DashboardSnapshot(
    DashboardKpis Kpis,
    IReadOnlyList<VehicleStatusCount> VehicleStatuses,
    IReadOnlyList<ExpiringItem> ExpiringItems,
    IReadOnlyList<AbnormalFuelItem> RecentAbnormalFuelLogs);

public sealed class DashboardService
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(int? branchId)
    {
        EnsureAccess();
        var user = Session.CurrentUser!;
        if (!user.IsHeadOffice && branchId != user.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }

        await using var db = DbContextFactory.Create();
        var today = DateTime.Now.Date;
        var todayDate = DateOnly.FromDateTime(today);
        var thirtyDaysFromToday = todayDate.AddDays(30);
        var sevenDaysFromToday = todayDate.AddDays(7);
        var thirtyDaysAgo = today.AddDays(-30);

        var vehicles = db.Vehicles.AsNoTracking();
        var drivers = db.Drivers.AsNoTracking();
        var trips = db.Trips.AsNoTracking();
        var fuelLogs = db.FuelLogs.AsNoTracking();
        var maintenance = db.Maintenances.AsNoTracking().ForCurrentBranch();
        var currentAssignments = db.VehicleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.EndDate == null);

        if (branchId.HasValue)
        {
            vehicles = vehicles.Where(vehicle => vehicle.BranchId == branchId.Value);
            drivers = drivers.Where(driver => driver.BranchId == branchId.Value);
            trips = trips.Where(trip =>
                trip.OriginBranchId == branchId.Value || trip.DestinationBranchId == branchId.Value);
            fuelLogs = fuelLogs.Where(log => log.Vehicle.BranchId == branchId.Value);
            maintenance = maintenance.Where(item => item.Vehicle.BranchId == branchId.Value);
            currentAssignments = currentAssignments.Where(assignment => assignment.Vehicle.BranchId == branchId.Value);
        }

        var kpis = new DashboardKpis(
            await vehicles.CountAsync(),
            await vehicles.CountAsync(vehicle => vehicle.Status == (int)VehicleStatus.Available),
            await vehicles.CountAsync(vehicle => vehicle.Status == (int)VehicleStatus.OnTrip),
            await vehicles.CountAsync(vehicle => vehicle.Status == (int)VehicleStatus.InMaintenance),
            await drivers.CountAsync(driver => driver.Status != (int)DriverStatus.Inactive),
            await drivers.CountAsync(driver => driver.Status == (int)DriverStatus.OnLeave),
            await vehicles.CountAsync(vehicle =>
                vehicle.Status != (int)VehicleStatus.Inactive
                && !currentAssignments.Any(assignment => assignment.VehicleId == vehicle.VehicleId)),
            await drivers.CountAsync(driver =>
                driver.Status != (int)DriverStatus.Inactive
                && !currentAssignments.Any(assignment => assignment.DriverId == driver.DriverId)),
            await trips.CountAsync(trip => trip.PlannedDeparture >= today && trip.PlannedDeparture < today.AddDays(1)),
            await trips.CountAsync(trip => trip.Status == (int)TripStatus.InTransit),
            await fuelLogs.CountAsync(log => log.IsAbnormal && log.FuelDate >= thirtyDaysAgo),
            await maintenance.CountAsync(item =>
                item.Status != (int)MaintenanceStatus.Completed
                && item.Status != (int)MaintenanceStatus.Cancelled
                && item.ScheduledDate <= sevenDaysFromToday),
            await vehicles.CountAsync(vehicle =>
                vehicle.RegistrationExpiry >= todayDate
                && vehicle.RegistrationExpiry <= thirtyDaysFromToday),
            await drivers.CountAsync(driver =>
                driver.LicenseExpiry >= todayDate
                && driver.LicenseExpiry <= thirtyDaysFromToday));

        var statusRows = await vehicles
            .GroupBy(vehicle => vehicle.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync();
        var vehicleStatuses = Enum.GetValues<VehicleStatus>()
            .Select(status => new VehicleStatusCount(
                GetStatusName(status),
                statusRows.SingleOrDefault(row => row.Status == (int)status)?.Count ?? 0))
            .ToList();

        var expiringVehicles = await vehicles
            .Where(vehicle =>
                vehicle.RegistrationExpiry >= todayDate
                && vehicle.RegistrationExpiry <= thirtyDaysFromToday)
            .Select(vehicle => new ExpiringItem(
                "Vehicle registration",
                vehicle.PlateNo,
                vehicle.Branch.BranchName,
                vehicle.RegistrationExpiry))
            .ToListAsync();
        var expiringDrivers = await drivers
            .Where(driver =>
                driver.LicenseExpiry >= todayDate
                && driver.LicenseExpiry <= thirtyDaysFromToday)
            .Select(driver => new ExpiringItem(
                "Driver license",
                driver.FullName,
                driver.Branch.BranchName,
                driver.LicenseExpiry))
            .ToListAsync();
        var expiringItems = expiringVehicles
            .Concat(expiringDrivers)
            .OrderBy(item => item.ExpiryDate)
            .ToList();

        var recentAbnormalFuelLogs = await fuelLogs
            .Where(log => log.IsAbnormal && log.FuelDate >= thirtyDaysAgo)
            .OrderByDescending(log => log.FuelDate)
            .Take(10)
            .Select(log => new AbnormalFuelItem(
                log.FuelDate,
                log.Vehicle.PlateNo,
                log.Vehicle.Branch.BranchName,
                log.Liters,
                log.TotalCost,
                log.Odometer))
            .ToListAsync();

        return new DashboardSnapshot(
            kpis,
            vehicleStatuses,
            expiringItems,
            recentAbnormalFuelLogs);
    }

    private static void EnsureAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (!Permissions.CanAccess(user.Role, Module.Dashboard))
        {
            throw new UnauthorizedAccessException("You do not have permission to view the dashboard.");
        }
    }

    private static string GetStatusName(VehicleStatus status) => status switch
    {
        VehicleStatus.Available => "Available",
        VehicleStatus.OnTrip => "On Trip",
        VehicleStatus.InMaintenance => "In Maintenance",
        VehicleStatus.Inactive => "Inactive",
        _ => status.ToString()
    };
}
