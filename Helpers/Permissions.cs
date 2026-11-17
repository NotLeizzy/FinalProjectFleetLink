using FinalProjectFleetLink.Models;

namespace FinalProjectFleetLink.Helpers;

public static class Permissions
{
    public static bool CanAccess(Role role, Module module)
    {
        return GetAccessLevel(role, module) != AccessLevel.None;
    }

    public static AccessLevel GetAccessLevel(Role role, Module module)
    {
        return role switch
        {
            Role.Administrator => module switch
            {
                Module.Dashboard or Module.Users or Module.Branches or Module.Reports => AccessLevel.Full,
                Module.Vehicles or Module.Drivers => AccessLevel.ReadOnly,
                _ => AccessLevel.None
            },
            Role.FleetManager => module is Module.Dashboard
                or Module.Vehicles
                or Module.Drivers
                or Module.Assignments
                or Module.FuelMonitor
                or Module.Reports
                ? AccessLevel.Full
                : module == Module.Maintenance
                    ? AccessLevel.ScheduleOnly
                    : AccessLevel.None,
            Role.Dispatcher => module is Module.Dashboard
                or Module.Shipments
                or Module.Trips
                or Module.Dispatch
                ? AccessLevel.Full
                : AccessLevel.None,
            Role.MaintenanceOfficer => module is Module.Dashboard or Module.Maintenance or Module.Reports
                ? AccessLevel.Full
                : AccessLevel.None,
            Role.FuelFinanceClerk => module is Module.Dashboard
                ? AccessLevel.Full
                : AccessLevel.None,
            _ => AccessLevel.None
        };
    }
}
