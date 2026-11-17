using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record BranchFilterOption(int? BranchId, string DisplayName);

public static class BranchFilterService
{
    public static async Task<IReadOnlyList<BranchFilterOption>> GetOptionsAsync(Module requiredModule)
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (!Permissions.CanAccess(user.Role, requiredModule))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this branch filter.");
        }

        await using var db = DbContextFactory.Create();
        var branches = db.Branches
            .AsNoTracking()
            .Where(branch => branch.IsActive);

        if (!user.IsHeadOffice)
        {
            branches = branches.Where(branch => branch.BranchId == user.BranchId);
        }

        var options = await branches
            .OrderBy(branch => branch.BranchCode)
            .Select(branch => new BranchFilterOption(branch.BranchId, branch.BranchName))
            .ToListAsync();

        if (user.IsHeadOffice)
        {
            options.Insert(0, new BranchFilterOption(null, "All branches"));
        }

        return options;
    }
}
