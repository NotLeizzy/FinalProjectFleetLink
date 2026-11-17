using FinalProjectFleetLink.Data;

namespace FinalProjectFleetLink.Helpers;

public static class BranchScope
{
    public static IQueryable<Vehicle> ForCurrentBranch(this IQueryable<Vehicle> query)
    {
        EnsureLoggedIn();
        return Session.IsHeadOffice
            ? query
            : query.Where(vehicle => vehicle.BranchId == Session.CurrentUser!.BranchId);
    }

    public static IQueryable<Driver> ForCurrentBranch(this IQueryable<Driver> query)
    {
        EnsureLoggedIn();
        return Session.IsHeadOffice
            ? query
            : query.Where(driver => driver.BranchId == Session.CurrentUser!.BranchId);
    }

    public static IQueryable<VehicleAssignment> ForCurrentBranch(this IQueryable<VehicleAssignment> query)
    {
        EnsureLoggedIn();
        return Session.IsHeadOffice
            ? query
            : query.Where(assignment => assignment.Vehicle.BranchId == Session.CurrentUser!.BranchId);
    }

    public static IQueryable<Shipment> ForCurrentBranch(this IQueryable<Shipment> query)
    {
        EnsureLoggedIn();
        return Session.IsHeadOffice
            ? query
            : query.Where(shipment => shipment.BranchId == Session.CurrentUser!.BranchId);
    }

    public static IQueryable<Trip> ForCurrentBranch(this IQueryable<Trip> query)
    {
        EnsureLoggedIn();
        return Session.IsHeadOffice
            ? query
            : query.Where(trip => trip.OriginBranchId == Session.CurrentUser!.BranchId);
    }

    public static IQueryable<Maintenance> ForCurrentBranch(this IQueryable<Maintenance> query)
    {
        EnsureLoggedIn();
        return Session.IsHeadOffice
            ? query
            : query.Where(maintenance => maintenance.Vehicle.BranchId == Session.CurrentUser!.BranchId);
    }

    private static void EnsureLoggedIn()
    {
        if (!Session.IsLoggedIn)
        {
            throw new UnauthorizedAccessException("A logged-in user is required for branch-scoped data.");
        }
    }
}
