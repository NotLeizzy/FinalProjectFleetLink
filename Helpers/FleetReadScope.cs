using FinalProjectFleetLink.Data;

namespace FinalProjectFleetLink.Helpers;

public static class FleetReadScope
{
    public static IQueryable<Vehicle> ReadVehicles(this IQueryable<Vehicle> query)
    {
        EnsureLoggedIn();
        return Session.IsHeadOffice
            ? query
            : query.Where(vehicle => vehicle.BranchId == Session.CurrentUser!.BranchId);
    }

    public static IQueryable<Driver> ReadDrivers(this IQueryable<Driver> query)
    {
        EnsureLoggedIn();
        return Session.IsHeadOffice
            ? query
            : query.Where(driver => driver.BranchId == Session.CurrentUser!.BranchId);
    }

    private static void EnsureLoggedIn()
    {
        if (!Session.IsLoggedIn)
        {
            throw new UnauthorizedAccessException("A logged-in user is required for branch-scoped data.");
        }
    }
}
