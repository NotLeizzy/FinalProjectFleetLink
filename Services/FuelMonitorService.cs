using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record FuelMonitorVehicleOption(int VehicleId, string PlateNo);

public sealed record FuelMonitorRow(
    DateTime FuelDate,
    string PlateNo,
    int? TripId,
    decimal Liters,
    decimal TotalCost,
    int Odometer,
    decimal? KmPerLiter,
    decimal ExpectedKmPerLiter,
    bool IsAbnormal);

public sealed record FuelMonitorSummary(decimal TotalLiters, decimal TotalCost, decimal AverageKmPerLiter);

public sealed record FuelMonitorResult(
    IReadOnlyList<FuelMonitorRow> Rows,
    FuelMonitorSummary Summary);

public sealed class FuelMonitorService
{
    public async Task<IReadOnlyList<FuelMonitorVehicleOption>> GetVehicleOptionsAsync(int? branchId)
    {
        EnsureAccess();
        ValidateBranch(branchId);
        await using var db = DbContextFactory.Create();
        var query = db.Vehicles.AsNoTracking().ForCurrentBranch();
        if (branchId.HasValue)
        {
            query = query.Where(vehicle => vehicle.BranchId == branchId.Value);
        }
        return await query.OrderBy(vehicle => vehicle.PlateNo)
            .Select(vehicle => new FuelMonitorVehicleOption(vehicle.VehicleId, vehicle.PlateNo))
            .ToListAsync();
    }

    public async Task<FuelMonitorResult> GetAsync(
        int? branchId,
        int? vehicleId,
        DateTime? startDate,
        DateTime? endDate,
        bool abnormalOnly)
    {
        EnsureAccess();
        ValidateBranch(branchId);
        if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
        {
            throw new ArgumentException("The start date cannot be after the end date.");
        }

        await using var db = DbContextFactory.Create();
        var query = db.FuelLogs.AsNoTracking().Where(log =>
            !branchId.HasValue || log.Vehicle.BranchId == branchId.Value);
        if (vehicleId.HasValue)
        {
            query = query.Where(log => log.VehicleId == vehicleId.Value);
        }
        if (startDate.HasValue)
        {
            query = query.Where(log => log.FuelDate >= startDate.Value.Date);
        }
        if (endDate.HasValue)
        {
            query = query.Where(log => log.FuelDate < endDate.Value.Date.AddDays(1));
        }
        if (abnormalOnly)
        {
            query = query.Where(log => log.IsAbnormal);
        }

        var rows = await query.OrderByDescending(log => log.FuelDate)
            .Select(log => new FuelMonitorRow(
                log.FuelDate,
                log.Vehicle.PlateNo,
                log.TripId,
                log.Liters,
                log.TotalCost,
                log.Odometer,
                log.KmPerLiter,
                log.Vehicle.ExpectedKmPerLiter,
                log.IsAbnormal))
            .ToListAsync();
        var average = rows.Where(row => row.KmPerLiter.HasValue)
            .Select(row => row.KmPerLiter!.Value)
            .DefaultIfEmpty()
            .Average();
        return new FuelMonitorResult(
            rows,
            new FuelMonitorSummary(rows.Sum(row => row.Liters), rows.Sum(row => row.TotalCost), average));
    }

    private static void ValidateBranch(int? branchId)
    {
        if (!Session.IsHeadOffice && branchId != Session.CurrentUser!.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }
    }

    private static void EnsureAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (!Permissions.CanAccess(user.Role, Module.FuelMonitor))
        {
            throw new UnauthorizedAccessException("You do not have permission to view fuel monitoring.");
        }
    }
}
