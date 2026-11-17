using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record VehicleLookupItem(
    int VehicleId,
    string PlateNo,
    VehicleType VehicleType,
    decimal CapacityKg,
    int BranchId,
    string BranchName,
    DateOnly RegistrationExpiry,
    int Status);

public sealed record DriverLookupItem(
    int DriverId,
    string FullName,
    string LicenseNo,
    int BranchId,
    string BranchName,
    DateOnly LicenseExpiry,
    int Status);

public sealed record BranchLookupItem(int BranchId, string BranchCode, string BranchName);

public sealed record CurrentDriverLookupItem(
    int DriverId,
    string FullName,
    string LicenseNo,
    DateOnly LicenseExpiry);

public sealed class LookupService
{
    public async Task<IReadOnlyList<VehicleLookupItem>> GetAvailableVehiclesAsync(int? branchId)
    {
        EnsureDispatcherAccess();
        ValidateBranch(branchId);
        await using var db = DbContextFactory.Create();
        var query = db.Vehicles
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(vehicle => vehicle.Status != (int)VehicleStatus.Inactive);
        if (branchId.HasValue)
        {
            query = query.Where(vehicle => vehicle.BranchId == branchId.Value);
        }

        return await query
            .OrderBy(vehicle => vehicle.PlateNo)
            .Select(vehicle => new VehicleLookupItem(
                vehicle.VehicleId,
                vehicle.PlateNo,
                (VehicleType)vehicle.VehicleType,
                vehicle.CapacityKg,
                vehicle.BranchId,
                vehicle.Branch.BranchName,
                vehicle.RegistrationExpiry,
                vehicle.Status))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DriverLookupItem>> GetActiveDriversAsync(int? branchId)
    {
        EnsureDispatcherAccess();
        ValidateBranch(branchId);
        await using var db = DbContextFactory.Create();
        var query = db.Drivers
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(driver => driver.Status != (int)DriverStatus.Inactive);
        if (branchId.HasValue)
        {
            query = query.Where(driver => driver.BranchId == branchId.Value);
        }

        return await query
            .OrderBy(driver => driver.FullName)
            .Select(driver => new DriverLookupItem(
                driver.DriverId,
                driver.FullName,
                driver.LicenseNo,
                driver.BranchId,
                driver.Branch.BranchName,
                driver.LicenseExpiry,
                driver.Status))
            .ToListAsync();
    }

    public async Task<CurrentDriverLookupItem?> GetCurrentDriverForVehicleAsync(int vehicleId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        var assignment = await db.VehicleAssignments
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(item => item.VehicleId == vehicleId && item.EndDate == null)
            .Select(item => new CurrentDriverLookupItem(
                item.DriverId,
                item.Driver.FullName,
                item.Driver.LicenseNo,
                item.Driver.LicenseExpiry))
            .SingleOrDefaultAsync();
        return assignment;
    }

    public async Task<IReadOnlyList<BranchLookupItem>> GetActiveBranchesAsync()
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        var query = db.Branches
            .AsNoTracking()
            .Where(branch => branch.IsActive);
        if (!Session.IsHeadOffice)
        {
            query = query.Where(branch => branch.BranchId == Session.CurrentUser!.BranchId);
        }

        return await query
            .OrderBy(branch => branch.BranchCode)
            .Select(branch => new BranchLookupItem(branch.BranchId, branch.BranchCode, branch.BranchName))
            .ToListAsync();
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
        if (Permissions.GetAccessLevel(user.Role, Module.Trips) == AccessLevel.None)
        {
            throw new UnauthorizedAccessException("Only Dispatchers can use dispatcher lookups.");
        }
    }
}
