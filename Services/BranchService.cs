using System.Text.RegularExpressions;
using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record BranchListItem(
    int BranchId,
    string BranchCode,
    string BranchName,
    string? Address,
    bool IsActive,
    int ActiveVehicles,
    int ActiveDrivers,
    int Users);

public sealed record BranchRequest(string BranchCode, string BranchName, string? Address);

public sealed record BranchDeactivationCheck(
    bool Exists,
    bool IsHeadOffice,
    int ActiveVehicles,
    int ActiveDrivers,
    int UnfinishedTrips,
    int ActiveUsers)
{
    public bool HasBlockingRecords => ActiveVehicles > 0 || ActiveDrivers > 0 || UnfinishedTrips > 0;
}

public sealed record BranchActionResult(
    bool Success,
    string? ErrorMessage = null,
    BranchDeactivationCheck? DeactivationCheck = null);

public sealed class BranchService
{
    private static readonly Regex BranchCodePattern = new("^[A-Z0-9]+$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<BranchListItem>> GetBranchesAsync(bool includeInactive)
    {
        EnsureAdministrator();
        var currentUser = Session.CurrentUser!;

        await using var db = DbContextFactory.Create();
        var query = db.Branches.AsNoTracking();

        if (!currentUser.IsHeadOffice)
        {
            query = query.Where(branch => branch.BranchId == currentUser.BranchId);
        }

        if (!includeInactive)
        {
            query = query.Where(branch => branch.IsActive);
        }

        return await query
            .OrderBy(branch => branch.BranchCode)
            .Select(branch => new BranchListItem(
                branch.BranchId,
                branch.BranchCode,
                branch.BranchName,
                branch.Address,
                branch.IsActive,
                branch.Vehicles.Count(vehicle => vehicle.Status != (int)VehicleStatus.Inactive),
                branch.Drivers.Count(driver => driver.Status != (int)DriverStatus.Inactive),
                branch.UserAccounts.Count()))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<BranchListItem>> GetActiveBranchesAsync()
    {
        var currentUser = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");

        await using var db = DbContextFactory.Create();
        var query = db.Branches
            .AsNoTracking()
            .Where(branch => branch.IsActive);
        if (!currentUser.IsHeadOffice)
        {
            query = query.Where(branch => branch.BranchId == currentUser.BranchId);
        }

        return await query
            .OrderBy(branch => branch.BranchCode)
            .Select(branch => new BranchListItem(
                branch.BranchId,
                branch.BranchCode,
                branch.BranchName,
                branch.Address,
                branch.IsActive,
                0,
                0,
                0))
            .ToListAsync();
    }

    public async Task<int> CreateAsync(BranchRequest request)
    {
        EnsureHeadOfficeAdministrator();
        var normalized = ValidateRequest(request);

        await using var db = DbContextFactory.Create();
        if (await BranchCodeExistsAsync(db, normalized.BranchCode, null))
        {
            throw new InvalidOperationException("BranchCode already exists.");
        }

        var branch = new Branch
        {
            BranchCode = normalized.BranchCode,
            BranchName = normalized.BranchName,
            Address = normalized.Address,
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.BranchId;
    }

    public async Task UpdateAsync(int branchId, BranchRequest request)
    {
        EnsureHeadOfficeAdministrator();
        var normalized = ValidateRequest(request);

        await using var db = DbContextFactory.Create();
        var branch = await db.Branches.SingleOrDefaultAsync(item => item.BranchId == branchId)
            ?? throw new InvalidOperationException("The selected branch was not found.");

        if (!string.Equals(branch.BranchCode, normalized.BranchCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("BranchCode cannot be changed after creation.");
        }

        if (await BranchCodeExistsAsync(db, normalized.BranchCode, branchId))
        {
            throw new InvalidOperationException("BranchCode already exists.");
        }

        branch.BranchName = normalized.BranchName;
        branch.Address = normalized.Address;
        await db.SaveChangesAsync();
    }

    public async Task<BranchDeactivationCheck> CheckDeactivationAsync(int branchId)
    {
        EnsureHeadOfficeAdministrator();
        await using var db = DbContextFactory.Create();
        return await BuildDeactivationCheckAsync(db, branchId);
    }

    public async Task<BranchActionResult> DeactivateAsync(int branchId, bool allowActiveUsers)
    {
        EnsureHeadOfficeAdministrator();
        await using var db = DbContextFactory.Create();
        var check = await BuildDeactivationCheckAsync(db, branchId);

        if (!check.Exists)
        {
            return new BranchActionResult(false, "The selected branch was not found.", check);
        }

        if (check.IsHeadOffice)
        {
            return new BranchActionResult(false, "The Head Office branch cannot be deactivated.", check);
        }

        if (check.HasBlockingRecords)
        {
            return new BranchActionResult(false, null, check);
        }

        if (check.ActiveUsers > 0 && !allowActiveUsers)
        {
            return new BranchActionResult(false, null, check);
        }

        var branch = await db.Branches.SingleAsync(item => item.BranchId == branchId);
        branch.IsActive = false;
        await db.SaveChangesAsync();
        return new BranchActionResult(true, null, check);
    }

    public async Task ActivateAsync(int branchId)
    {
        EnsureHeadOfficeAdministrator();
        await using var db = DbContextFactory.Create();
        var branch = await db.Branches.SingleOrDefaultAsync(item => item.BranchId == branchId)
            ?? throw new InvalidOperationException("The selected branch was not found.");
        branch.IsActive = true;
        await db.SaveChangesAsync();
    }

    private static async Task<BranchDeactivationCheck> BuildDeactivationCheckAsync(
        FleetLinkDbContext db,
        int branchId)
    {
        var branch = await db.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.BranchId == branchId);
        if (branch is null)
        {
            return new BranchDeactivationCheck(false, false, 0, 0, 0, 0);
        }

        var unfinishedStatuses = new[]
        {
            (int)TripStatus.Scheduled,
            (int)TripStatus.Dispatched,
            (int)TripStatus.InTransit
        };

        var activeVehicles = await db.Vehicles.CountAsync(vehicle =>
            vehicle.BranchId == branchId && vehicle.Status != (int)VehicleStatus.Inactive);
        var activeDrivers = await db.Drivers.CountAsync(driver =>
            driver.BranchId == branchId && driver.Status != (int)DriverStatus.Inactive);
        var unfinishedTrips = await db.Trips.CountAsync(trip =>
            unfinishedStatuses.Contains(trip.Status)
            && (trip.OriginBranchId == branchId || trip.DestinationBranchId == branchId));
        var activeUsers = await db.UserAccounts.CountAsync(user =>
            user.BranchId == branchId && user.IsActive);

        return new BranchDeactivationCheck(
            true,
            string.Equals(branch.BranchCode, "HO", StringComparison.OrdinalIgnoreCase),
            activeVehicles,
            activeDrivers,
            unfinishedTrips,
            activeUsers);
    }

    private static BranchRequest ValidateRequest(BranchRequest request)
    {
        var code = request.BranchCode.Trim().ToUpperInvariant();
        var name = request.BranchName.Trim();
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        if (code.Length == 0 || code.Length > 10 || !BranchCodePattern.IsMatch(code))
        {
            throw new ArgumentException("BranchCode is required, must be at most 10 characters, and may contain only letters and numbers.");
        }
        if (name.Length == 0 || name.Length > 100)
        {
            throw new ArgumentException("BranchName is required and must be at most 100 characters.");
        }
        if (address is not null && address.Length > 200)
        {
            throw new ArgumentException("Address must be at most 200 characters.");
        }

        return new BranchRequest(code, name, address);
    }

    private static async Task<bool> BranchCodeExistsAsync(
        FleetLinkDbContext db,
        string branchCode,
        int? excludedBranchId)
    {
        return await db.Branches.AnyAsync(branch =>
            branch.BranchCode.ToUpper() == branchCode
            && (!excludedBranchId.HasValue || branch.BranchId != excludedBranchId.Value));
    }

    private static void EnsureAdministrator()
    {
        if (Session.CurrentUser?.Role != Role.Administrator)
        {
            throw new UnauthorizedAccessException("Only administrators can access branch management.");
        }
    }

    private static void EnsureHeadOfficeAdministrator()
    {
        EnsureAdministrator();
        if (!Session.CurrentUser!.IsHeadOffice)
        {
            throw new UnauthorizedAccessException("Only head-office administrators can modify branches.");
        }
    }
}
