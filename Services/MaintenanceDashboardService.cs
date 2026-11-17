using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record MaintenanceDashboardKpis(
    int Overdue,
    int DueWithinSevenDays,
    int DueWithinFiveHundredKm,
    int InProgress,
    int CompletedThisMonth,
    decimal CostThisMonth);

public sealed record MaintenanceUrgencyItem(
    int MaintenanceId,
    string PlateNo,
    VehicleType VehicleType,
    MaintenanceType MaintenanceType,
    DateOnly ScheduledDate,
    int? DueOdometer,
    int CurrentOdometer,
    MaintenanceDueState DueState,
    int DaysOverdue,
    int KmOverdue,
    string BranchName);

public sealed record MaintenanceWorkshopItem(
    int MaintenanceId,
    string PlateNo,
    VehicleType VehicleType,
    MaintenanceType MaintenanceType,
    DateOnly ScheduledDate,
    string BranchName);

public sealed record MaintenanceCostMonth(string Month, decimal Cost);

public sealed record MaintenanceDashboardSnapshot(
    MaintenanceDashboardKpis Kpis,
    IReadOnlyList<MaintenanceUrgencyItem> VehiclesToService,
    IReadOnlyList<MaintenanceWorkshopItem> CurrentlyInWorkshop,
    IReadOnlyList<MaintenanceCostMonth> MonthlyCosts);

public sealed class MaintenanceDashboardService
{
    public async Task<MaintenanceDashboardSnapshot> GetSnapshotAsync(int? branchId)
    {
        EnsureAccess();
        ValidateBranch(branchId);

        await using var db = DbContextFactory.Create();
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        IQueryable<Maintenance> maintenance = db.Maintenances.AsNoTracking().ForCurrentBranch().Include(item => item.Vehicle).Include(item => item.Vehicle.Branch);
        if (branchId.HasValue)
        {
            maintenance = maintenance.Where(item => item.Vehicle.BranchId == branchId.Value);
        }

        var records = await maintenance.ToListAsync();
        var overdue = records.Where(item => MaintenanceDueHelper.IsBlocking(item, item.Vehicle, now)).ToList();
        var active = records.Where(item => item.Status == (int)MaintenanceStatus.Scheduled || item.Status == (int)MaintenanceStatus.InProgress).ToList();
        var dueDate = today.AddDays(MaintenanceDueSettings.DueSoonDays);
        var dueOdometer = records.Where(item => item.DueOdometer.HasValue && item.DueOdometer.Value > item.Vehicle.CurrentOdometer && item.DueOdometer.Value <= item.Vehicle.CurrentOdometer + MaintenanceDueSettings.DueSoonOdometerKm).ToList();
        var completedThisMonth = records.Where(item => item.Status == (int)MaintenanceStatus.Completed && item.CompletedDate >= monthStart && item.CompletedDate <= today).ToList();
        var urgency = active
            .Where(item => overdue.Contains(item) || item.ScheduledDate <= dueDate || dueOdometer.Contains(item))
            .Select(item => ToUrgency(item, today))
            .OrderByDescending(item => item.DueState == MaintenanceDueState.Overdue)
            .ThenByDescending(item => Math.Max(item.DaysOverdue, item.KmOverdue))
            .ThenBy(item => item.ScheduledDate)
            .ToList();
        var workshop = active
            .Where(item => item.Status == (int)MaintenanceStatus.InProgress)
            .Select(item => new MaintenanceWorkshopItem(item.MaintenanceId, item.Vehicle.PlateNo, (VehicleType)item.Vehicle.VehicleType, (MaintenanceType)item.MaintenanceType, item.ScheduledDate, item.Vehicle.Branch.BranchName))
            .OrderBy(item => item.ScheduledDate)
            .ToList();

        var sixMonthsStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
        var monthlyCosts = Enumerable.Range(0, 6)
            .Select(offset => sixMonthsStart.AddMonths(offset))
            .Select(month => new MaintenanceCostMonth(month.ToString("yyyy-MM"), records
                .Where(item => item.Status == (int)MaintenanceStatus.Completed && item.CompletedDate.HasValue && item.CompletedDate.Value.Year == month.Year && item.CompletedDate.Value.Month == month.Month)
                .Sum(item => item.Cost ?? 0)))
            .ToList();

        return new MaintenanceDashboardSnapshot(
            new MaintenanceDashboardKpis(
                overdue.Count,
                active.Count(item => item.ScheduledDate >= today && item.ScheduledDate <= dueDate),
                dueOdometer.Count,
                records.Count(item => item.Status == (int)MaintenanceStatus.InProgress),
                completedThisMonth.Count,
                completedThisMonth.Sum(item => item.Cost ?? 0)),
            urgency,
            workshop,
            monthlyCosts);
    }

    private static MaintenanceUrgencyItem ToUrgency(Maintenance item, DateOnly today)
    {
        var overdueDate = item.ScheduledDate < today ? today.DayNumber - item.ScheduledDate.DayNumber : 0;
        var overdueKm = item.DueOdometer.HasValue && item.DueOdometer.Value < item.Vehicle.CurrentOdometer
            ? item.Vehicle.CurrentOdometer - item.DueOdometer.Value
            : 0;
        return new MaintenanceUrgencyItem(item.MaintenanceId, item.Vehicle.PlateNo, (VehicleType)item.Vehicle.VehicleType, (MaintenanceType)item.MaintenanceType, item.ScheduledDate, item.DueOdometer, item.Vehicle.CurrentOdometer, MaintenanceDueHelper.GetDueState(item, item.Vehicle), overdueDate, overdueKm, item.Vehicle.Branch.BranchName);
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
        var user = Session.CurrentUser ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Maintenance) == AccessLevel.None)
        {
            throw new UnauthorizedAccessException("You do not have permission to view maintenance data.");
        }
    }
}
