using System.Globalization;
using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record ReportResult(
    string Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed class ReportsService
{
    public async Task<ReportResult> GetReportAsync(
        ReportType reportType,
        int? branchId,
        DateTime? startDate,
        DateTime? endDate,
        int? maintenanceVehicleId = null,
        MaintenanceType? maintenanceType = null,
        MaintenanceStatus? maintenanceStatus = null)
    {
        EnsureAccess();
        var user = Session.CurrentUser!;
        if (!user.IsHeadOffice && branchId != user.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }
        if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
        {
            throw new ArgumentException("The start date cannot be after the end date.");
        }

        await using var db = DbContextFactory.Create();
        return reportType switch
        {
            ReportType.VehicleList => await GetVehicleReportAsync(db, branchId),
            ReportType.DriverList => await GetDriverReportAsync(db, branchId),
            ReportType.TripsSummary => await GetTripReportAsync(db, branchId, startDate, endDate),
            ReportType.FuelUsage => await GetFuelReportAsync(db, branchId, startDate, endDate),
            ReportType.MaintenanceHistory => await GetMaintenanceReportAsync(db, branchId, startDate, endDate, maintenanceVehicleId, maintenanceType, maintenanceStatus),
            ReportType.MaintenanceCostPerVehicle => await GetMaintenanceCostReportAsync(db, branchId, startDate, endDate),
            ReportType.OverdueUpcomingMaintenance => await GetOverdueUpcomingMaintenanceReportAsync(db, branchId),
            ReportType.VehiclesWithoutRecentMaintenance => await GetVehiclesWithoutRecentMaintenanceReportAsync(db, branchId),
            ReportType.UserAccounts => await GetUserReportAsync(db, branchId),
            ReportType.CurrentAssignments => await GetAssignmentReportAsync(db, branchId, false, startDate, endDate),
            ReportType.AssignmentHistory => await GetAssignmentReportAsync(db, branchId, true, startDate, endDate),
            ReportType.ExpiringDocuments => await GetExpiringDocumentsReportAsync(db, branchId, endDate),
            ReportType.FuelConsumptionSummary => await GetFuelConsumptionSummaryReportAsync(db, branchId, startDate, endDate),
            _ => throw new ArgumentOutOfRangeException(nameof(reportType))
        };
    }

    private static async Task<ReportResult> GetMaintenanceCostReportAsync(FleetLinkDbContext db, int? branchId, DateTime? startDate, DateTime? endDate)
    {
        var query = db.Maintenances.AsNoTracking().ForCurrentBranch().Where(item => item.Status == (int)MaintenanceStatus.Completed && item.CompletedDate.HasValue);
        if (branchId.HasValue) query = query.Where(item => item.Vehicle.BranchId == branchId.Value);
        if (startDate.HasValue) query = query.Where(item => item.CompletedDate >= DateOnly.FromDateTime(startDate.Value.Date));
        if (endDate.HasValue) query = query.Where(item => item.CompletedDate <= DateOnly.FromDateTime(endDate.Value.Date));
        var rows = await query.GroupBy(item => new { item.VehicleId, Vehicle = item.Vehicle.PlateNo, Branch = item.Vehicle.Branch.BranchName }).Select(group => new { group.Key.Vehicle, group.Key.Branch, Cost = group.Sum(item => item.Cost ?? 0), Services = group.Count() }).OrderBy(item => item.Vehicle).ToListAsync();
        return new ReportResult("Maintenance cost per vehicle", ["Vehicle", "Branch", "Services", "Total Cost"], rows.Select(item => (IReadOnlyList<string>)[item.Vehicle, item.Branch, item.Services.ToString(CultureInfo.InvariantCulture), item.Cost.ToString("0.##", CultureInfo.InvariantCulture)]).ToList());
    }

    private static async Task<ReportResult> GetOverdueUpcomingMaintenanceReportAsync(FleetLinkDbContext db, int? branchId)
    {
        var records = await db.Maintenances.AsNoTracking().ForCurrentBranch().Include(item => item.Vehicle).Include(item => item.Vehicle.Branch).Where(item => !branchId.HasValue || item.Vehicle.BranchId == branchId.Value).ToListAsync();
        var rows = records.Where(item => MaintenanceDueHelper.GetDueState(item, item.Vehicle) is MaintenanceDueState.Overdue or MaintenanceDueState.DueSoon).Select(item => new
        {
            Vehicle = item.Vehicle.PlateNo,
            Branch = item.Vehicle.Branch.BranchName,
            Type = GetMaintenanceTypeName(item.MaintenanceType),
            Status = GetMaintenanceStatusName(item.Status),
            DueState = MaintenanceDueHelper.GetDueState(item, item.Vehicle).ToString(),
            DueDate = item.ScheduledDate,
            item.DueOdometer,
            item.Vehicle.CurrentOdometer
        }).OrderBy(item => item.DueDate).ToList();
        return new ReportResult("Overdue and upcoming maintenance", ["Vehicle", "Branch", "Type", "Status", "Due State", "Scheduled Date", "Due Odometer", "Current Odometer"], rows.Select(item => (IReadOnlyList<string>)[item.Vehicle, item.Branch, item.Type, item.Status, item.DueState, item.DueDate.ToString("yyyy-MM-dd"), item.DueOdometer?.ToString(CultureInfo.InvariantCulture) ?? "", item.CurrentOdometer.ToString(CultureInfo.InvariantCulture)]).ToList());
    }

    private static async Task<ReportResult> GetVehiclesWithoutRecentMaintenanceReportAsync(FleetLinkDbContext db, int? branchId)
    {
        var since = DateOnly.FromDateTime(DateTime.Now.Date).AddMonths(-6);
        var vehicles = await db.Vehicles.AsNoTracking().ForCurrentBranch().Where(item => !branchId.HasValue || item.BranchId == branchId.Value).Where(item => !item.Maintenances.Any(maintenance => maintenance.Status == (int)MaintenanceStatus.Completed && maintenance.CompletedDate >= since)).OrderBy(item => item.PlateNo).Select(item => new { item.PlateNo, Branch = item.Branch.BranchName, item.CurrentOdometer }).ToListAsync();
        return new ReportResult("Vehicles without maintenance in last 6 months", ["Vehicle", "Branch", "Current Odometer"], vehicles.Select(item => (IReadOnlyList<string>)[item.PlateNo, item.Branch, item.CurrentOdometer.ToString(CultureInfo.InvariantCulture)]).ToList());
    }

    private static async Task<ReportResult> GetAssignmentReportAsync(FleetLinkDbContext db, int? branchId, bool history, DateTime? startDate, DateTime? endDate)
    {
        var query = db.VehicleAssignments.AsNoTracking().Where(item => !branchId.HasValue || item.Vehicle.BranchId == branchId.Value);
        query = history ? query.Where(item => item.EndDate != null) : query.Where(item => item.EndDate == null);
        if (startDate.HasValue) query = query.Where(item => item.StartDate >= DateOnly.FromDateTime(startDate.Value.Date));
        if (endDate.HasValue) query = query.Where(item => item.StartDate <= DateOnly.FromDateTime(endDate.Value.Date));
        var rows = await query.OrderByDescending(item => item.StartDate).Select(item => new
        {
            item.StartDate, item.EndDate, Vehicle = item.Vehicle.PlateNo, Driver = item.Driver.FullName, Branch = item.Vehicle.Branch.BranchName
        }).ToListAsync();
        return new ReportResult(history ? "Assignment history" : "Current assignments", ["Vehicle", "Driver", "Branch", "Start Date", "End Date"], rows.Select(item => (IReadOnlyList<string>)[item.Vehicle, item.Driver, item.Branch, item.StartDate.ToString("yyyy-MM-dd"), item.EndDate?.ToString("yyyy-MM-dd") ?? ""]).ToList());
    }

    private static async Task<ReportResult> GetExpiringDocumentsReportAsync(FleetLinkDbContext db, int? branchId, DateTime? endDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var until = DateOnly.FromDateTime((endDate ?? DateTime.Today.AddDays(30)).Date);
        var vehicles = await db.Vehicles.AsNoTracking().Where(item => (!branchId.HasValue || item.BranchId == branchId.Value) && item.RegistrationExpiry >= today && item.RegistrationExpiry <= until).Select(item => new { Type = "Registration", Name = item.PlateNo, Branch = item.Branch.BranchName, item.RegistrationExpiry }).ToListAsync();
        var drivers = await db.Drivers.AsNoTracking().Where(item => (!branchId.HasValue || item.BranchId == branchId.Value) && item.LicenseExpiry >= today && item.LicenseExpiry <= until).Select(item => new { Type = "License", Name = item.FullName, Branch = item.Branch.BranchName, Expiry = item.LicenseExpiry }).ToListAsync();
        var rows = vehicles.Select(item => (Type: item.Type, item.Name, item.Branch, Expiry: item.RegistrationExpiry)).Concat(drivers.Select(item => (item.Type, item.Name, item.Branch, item.Expiry))).OrderBy(item => item.Expiry).ToList();
        return new ReportResult("Expiring documents", ["Document", "Vehicle/Driver", "Branch", "Expiry Date"], rows.Select(item => (IReadOnlyList<string>)[item.Type, item.Name, item.Branch, item.Expiry.ToString("yyyy-MM-dd")]).ToList());
    }

    private static async Task<ReportResult> GetFuelConsumptionSummaryReportAsync(FleetLinkDbContext db, int? branchId, DateTime? startDate, DateTime? endDate)
    {
        var query = db.FuelLogs.AsNoTracking().Where(item => !branchId.HasValue || item.Vehicle.BranchId == branchId.Value);
        query = ApplyDateRange(query, item => item.FuelDate, startDate, endDate);
        var rows = await query.GroupBy(item => new { item.VehicleId, Plate = item.Vehicle.PlateNo, Branch = item.Vehicle.Branch.BranchName }).Select(group => new { group.Key.Plate, group.Key.Branch, Liters = group.Sum(item => item.Liters), Cost = group.Sum(item => item.TotalCost), Average = group.Where(item => item.KmPerLiter.HasValue).Average(item => (decimal?)item.KmPerLiter) }).OrderBy(item => item.Plate).ToListAsync();
        return new ReportResult("Fuel consumption summary", ["Vehicle", "Branch", "Total Liters", "Total Cost", "Average Km/L"], rows.Select(item => (IReadOnlyList<string>)[item.Plate, item.Branch, item.Liters.ToString("0.##", CultureInfo.InvariantCulture), item.Cost.ToString("0.##", CultureInfo.InvariantCulture), item.Average?.ToString("0.##", CultureInfo.InvariantCulture) ?? ""]).ToList());
    }

    private static async Task<ReportResult> GetVehicleReportAsync(FleetLinkDbContext db, int? branchId)
    {
        var query = db.Vehicles.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(vehicle => vehicle.BranchId == branchId.Value);
        }

        var rows = await query
            .OrderBy(vehicle => vehicle.PlateNo)
            .Select(vehicle => new
            {
                vehicle.PlateNo,
                Branch = vehicle.Branch.BranchName,
                vehicle.VehicleType,
                vehicle.Status,
                vehicle.MakeModel,
                vehicle.CapacityKg,
                vehicle.RegistrationExpiry,
                vehicle.CurrentOdometer
            })
            .ToListAsync();

        return new ReportResult(
            "Vehicle list",
            ["Plate No", "Branch", "Vehicle Type", "Status", "Make/Model", "Capacity Kg", "Registration Expiry", "Current Odometer"],
            rows.Select(vehicle => (IReadOnlyList<string>)[
                vehicle.PlateNo,
                vehicle.Branch,
                GetVehicleTypeName(vehicle.VehicleType),
                GetVehicleStatusName(vehicle.Status),
                vehicle.MakeModel ?? string.Empty,
                vehicle.CapacityKg.ToString("0.##", CultureInfo.InvariantCulture),
                vehicle.RegistrationExpiry.ToString("yyyy-MM-dd"),
                vehicle.CurrentOdometer.ToString(CultureInfo.InvariantCulture)
            ]).ToList());
    }

    private static async Task<ReportResult> GetDriverReportAsync(FleetLinkDbContext db, int? branchId)
    {
        var query = db.Drivers.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(driver => driver.BranchId == branchId.Value);
        }

        var rows = await query
            .OrderBy(driver => driver.FullName)
            .Select(driver => new
            {
                driver.FullName,
                Branch = driver.Branch.BranchName,
                driver.LicenseNo,
                driver.LicenseExpiry,
                driver.ContactNo,
                driver.Status
            })
            .ToListAsync();

        return new ReportResult(
            "Driver list",
            ["Full Name", "Branch", "License No", "License Expiry", "Contact No", "Status"],
            rows.Select(driver => (IReadOnlyList<string>)[
                driver.FullName,
                driver.Branch,
                driver.LicenseNo,
                driver.LicenseExpiry.ToString("yyyy-MM-dd"),
                driver.ContactNo ?? string.Empty,
                GetDriverStatusName(driver.Status)
            ]).ToList());
    }

    private static async Task<ReportResult> GetTripReportAsync(
        FleetLinkDbContext db,
        int? branchId,
        DateTime? startDate,
        DateTime? endDate)
    {
        var query = db.Trips.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(trip =>
                trip.OriginBranchId == branchId.Value || trip.DestinationBranchId == branchId.Value);
        }
        query = ApplyDateRange(query, trip => trip.PlannedDeparture, startDate, endDate);

        var rows = await query
            .OrderByDescending(trip => trip.PlannedDeparture)
            .Select(trip => new
            {
                trip.TripId,
                Vehicle = trip.Vehicle.PlateNo,
                Driver = trip.Driver.FullName,
                OriginBranch = trip.OriginBranch.BranchName,
                DestinationBranch = trip.DestinationBranch == null ? string.Empty : trip.DestinationBranch.BranchName,
                TripOrigin = trip.Origin,
                trip.Destination,
                trip.PlannedDeparture,
                trip.PlannedArrival,
                trip.Status
            })
            .ToListAsync();

        return new ReportResult(
            "Trips summary",
            ["Trip ID", "Vehicle", "Driver", "Origin Branch", "Destination Branch", "Origin", "Destination", "Planned Departure", "Planned Arrival", "Status"],
            rows.Select(trip => (IReadOnlyList<string>)[
                trip.TripId.ToString(CultureInfo.InvariantCulture),
                trip.Vehicle,
                trip.Driver,
                trip.OriginBranch,
                trip.DestinationBranch,
                trip.TripOrigin,
                trip.Destination,
                trip.PlannedDeparture.ToString("yyyy-MM-dd HH:mm"),
                trip.PlannedArrival.ToString("yyyy-MM-dd HH:mm"),
                GetTripStatusName(trip.Status)
            ]).ToList());
    }

    private static async Task<ReportResult> GetFuelReportAsync(
        FleetLinkDbContext db,
        int? branchId,
        DateTime? startDate,
        DateTime? endDate)
    {
        var query = db.FuelLogs.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(log => log.Vehicle.BranchId == branchId.Value);
        }
        query = ApplyDateRange(query, log => log.FuelDate, startDate, endDate);

        var rows = await query
            .OrderByDescending(log => log.FuelDate)
            .Select(log => new
            {
                log.FuelDate,
                Vehicle = log.Vehicle.PlateNo,
                Branch = log.Vehicle.Branch.BranchName,
                log.TripId,
                log.Liters,
                log.TotalCost,
                log.Odometer,
                log.KmPerLiter,
                log.IsAbnormal
            })
            .ToListAsync();

        return new ReportResult(
            "Fuel usage",
            ["Fuel Date", "Vehicle", "Branch", "Trip ID", "Liters", "Total Cost", "Odometer", "Km/L", "Abnormal"],
            rows.Select(log => (IReadOnlyList<string>)[
                log.FuelDate.ToString("yyyy-MM-dd HH:mm"),
                log.Vehicle,
                log.Branch,
                log.TripId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                log.Liters.ToString("0.##", CultureInfo.InvariantCulture),
                log.TotalCost.ToString("0.##", CultureInfo.InvariantCulture),
                log.Odometer.ToString(CultureInfo.InvariantCulture),
                log.KmPerLiter?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
                log.IsAbnormal ? "Yes" : "No"
            ]).ToList());
    }

    private static async Task<ReportResult> GetMaintenanceReportAsync(
        FleetLinkDbContext db,
        int? branchId,
        DateTime? startDate,
        DateTime? endDate,
        int? vehicleId,
        MaintenanceType? maintenanceType,
        MaintenanceStatus? maintenanceStatus)
    {
        var query = db.Maintenances.AsNoTracking().ForCurrentBranch().AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(item => item.Vehicle.BranchId == branchId.Value);
        }
        if (vehicleId.HasValue)
        {
            query = query.Where(item => item.VehicleId == vehicleId.Value);
        }
        if (maintenanceType.HasValue)
        {
            query = query.Where(item => item.MaintenanceType == (int)maintenanceType.Value);
        }
        if (maintenanceStatus.HasValue)
        {
            query = query.Where(item => item.Status == (int)maintenanceStatus.Value);
        }
        if (startDate.HasValue)
        {
            var start = DateOnly.FromDateTime(startDate.Value.Date);
            query = query.Where(item => item.ScheduledDate >= start);
        }
        if (endDate.HasValue)
        {
            var end = DateOnly.FromDateTime(endDate.Value.Date);
            query = query.Where(item => item.ScheduledDate <= end);
        }

        var rows = await query
            .OrderByDescending(item => item.ScheduledDate)
            .Select(item => new
            {
                item.ScheduledDate,
                Vehicle = item.Vehicle.PlateNo,
                Branch = item.Vehicle.Branch.BranchName,
                item.MaintenanceType,
                item.Status,
                item.DueOdometer,
                item.CompletedDate,
                item.Cost,
                item.Notes
            })
            .ToListAsync();

        return new ReportResult(
            "Maintenance history",
            ["Scheduled Date", "Vehicle", "Branch", "Maintenance Type", "Status", "Due Odometer", "Completed Date", "Cost", "Notes"],
            rows.Select(item => (IReadOnlyList<string>)[
                item.ScheduledDate.ToString("yyyy-MM-dd"),
                item.Vehicle,
                item.Branch,
                GetMaintenanceTypeName(item.MaintenanceType),
                GetMaintenanceStatusName(item.Status),
                item.DueOdometer?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.CompletedDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                item.Cost?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
                item.Notes ?? string.Empty
            ]).ToList());
    }

    private static async Task<ReportResult> GetUserReportAsync(FleetLinkDbContext db, int? branchId)
    {
        var query = db.UserAccounts.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(user => user.BranchId == branchId.Value);
        }

        var rows = await query
            .OrderBy(user => user.Email)
            .Select(user => new
            {
                user.Email,
                user.FullName,
                user.Role,
                Branch = user.Branch == null ? "Head Office" : user.Branch.BranchName,
                user.IsActive,
                user.CreatedAt
            })
            .ToListAsync();

        return new ReportResult(
            "User accounts",
            ["Email", "Full Name", "Role", "Branch", "Active", "Created At"],
            rows.Select(user => (IReadOnlyList<string>)[
                user.Email,
                user.FullName,
                GetRoleName(user.Role),
                user.Branch,
                user.IsActive ? "Yes" : "No",
                user.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            ]).ToList());
    }

    private static IQueryable<T> ApplyDateRange<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, DateTime>> selector,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue)
        {
            var start = startDate.Value.Date;
            query = query.Where(BuildGreaterThanOrEqual(selector, start));
        }
        if (endDate.HasValue)
        {
            var endExclusive = endDate.Value.Date.AddDays(1);
            query = query.Where(BuildLessThan(selector, endExclusive));
        }
        return query;
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildGreaterThanOrEqual<T>(
        System.Linq.Expressions.Expression<Func<T, DateTime>> selector,
        DateTime value)
    {
        var parameter = selector.Parameters[0];
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.GreaterThanOrEqual(
                selector.Body,
                System.Linq.Expressions.Expression.Constant(value)),
            parameter);
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildLessThan<T>(
        System.Linq.Expressions.Expression<Func<T, DateTime>> selector,
        DateTime value)
    {
        var parameter = selector.Parameters[0];
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.LessThan(
                selector.Body,
                System.Linq.Expressions.Expression.Constant(value)),
            parameter);
    }

    private static void EnsureAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (!Permissions.CanAccess(user.Role, Module.Reports))
        {
            throw new UnauthorizedAccessException("You do not have permission to view reports.");
        }
    }

    private static string GetRoleName(int role) => Enum.IsDefined(typeof(Role), role)
        ? ((Role)role) switch
        {
            Role.Administrator => "Administrator",
            Role.FleetManager => "Fleet Manager",
            Role.Dispatcher => "Dispatcher",
            Role.MaintenanceOfficer => "Maintenance Officer",
            Role.FuelFinanceClerk => "Fuel/Finance Clerk",
            _ => role.ToString(CultureInfo.InvariantCulture)
        }
        : role.ToString(CultureInfo.InvariantCulture);

    private static string GetVehicleTypeName(int value) => Enum.IsDefined(typeof(VehicleType), value)
        ? ((VehicleType)value).ToString()
        : value.ToString(CultureInfo.InvariantCulture);

    private static string GetVehicleStatusName(int value) => Enum.IsDefined(typeof(VehicleStatus), value)
        ? ((VehicleStatus)value) switch
        {
            VehicleStatus.Available => "Available",
            VehicleStatus.OnTrip => "On Trip",
            VehicleStatus.InMaintenance => "In Maintenance",
            VehicleStatus.Inactive => "Inactive",
            _ => value.ToString(CultureInfo.InvariantCulture)
        }
        : value.ToString(CultureInfo.InvariantCulture);

    private static string GetDriverStatusName(int value) => Enum.IsDefined(typeof(DriverStatus), value)
        ? ((DriverStatus)value).ToString()
        : value.ToString(CultureInfo.InvariantCulture);

    private static string GetTripStatusName(int value) => Enum.IsDefined(typeof(TripStatus), value)
        ? ((TripStatus)value).ToString()
        : value.ToString(CultureInfo.InvariantCulture);

    private static string GetMaintenanceTypeName(int value) => Enum.IsDefined(typeof(MaintenanceType), value)
        ? ((MaintenanceType)value).ToString()
        : value.ToString(CultureInfo.InvariantCulture);

    private static string GetMaintenanceStatusName(int value) => Enum.IsDefined(typeof(MaintenanceStatus), value)
        ? ((MaintenanceStatus)value).ToString()
        : value.ToString(CultureInfo.InvariantCulture);
}
