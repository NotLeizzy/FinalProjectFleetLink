using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Models;

namespace FinalProjectFleetLink.Helpers;

public enum MaintenanceDueState
{
    Ok,
    DueSoon,
    Overdue
}

public static class MaintenanceDueHelper
{
    public static bool IsBlocking(Maintenance maintenance, Vehicle vehicle, DateTime checkDate)
    {
        var checkDateOnly = DateOnly.FromDateTime(checkDate);
        return maintenance.Status is (int)MaintenanceStatus.Scheduled or (int)MaintenanceStatus.InProgress
            && (maintenance.ScheduledDate <= checkDateOnly
                || maintenance.DueOdometer.HasValue
                    && maintenance.DueOdometer.Value <= vehicle.CurrentOdometer);
    }

    public static MaintenanceDueState GetDueState(Maintenance maintenance, Vehicle vehicle)
    {
        if (IsBlocking(maintenance, vehicle, DateTime.Now))
        {
            return MaintenanceDueState.Overdue;
        }

        var dueSoonDate = DateOnly.FromDateTime(DateTime.Now).AddDays(MaintenanceDueSettings.DueSoonDays);
        var dueSoonOdometer = vehicle.CurrentOdometer + MaintenanceDueSettings.DueSoonOdometerKm;
        var dueSoon = maintenance.Status is (int)MaintenanceStatus.Scheduled or (int)MaintenanceStatus.InProgress
            && (maintenance.ScheduledDate <= dueSoonDate
                || maintenance.DueOdometer.HasValue && maintenance.DueOdometer.Value <= dueSoonOdometer);
        return dueSoon ? MaintenanceDueState.DueSoon : MaintenanceDueState.Ok;
    }

    public static Color GetRowColor(MaintenanceDueState state) => state switch
    {
        MaintenanceDueState.Overdue => Color.MistyRose,
        MaintenanceDueState.DueSoon => Color.Moccasin,
        _ => Color.White
    };
}
