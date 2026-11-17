using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Helpers;

public static class DatabaseSeeder
{
    public const string DefaultEmail = "admin@fleetlink.local";
    public const string DefaultPassword = "Admin@123"; // TODO: Change the default administrator password after first login.

    public static async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var connection = new DbConnections();
        var options = new DbContextOptionsBuilder<FleetLinkDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;

        await using var db = new FleetLinkDbContext(options);

        if (!await db.Branches.AnyAsync(cancellationToken))
        {
            db.Branches.Add(new Branch
            {
                BranchCode = "HO",
                BranchName = "Head Office",
                IsActive = true
            });
        }

        if (!await db.UserAccounts.AnyAsync(cancellationToken))
        {
            db.UserAccounts.Add(new UserAccount
            {
                Email = DefaultEmail,
                PasswordHash = PasswordHasher.Hash(DefaultPassword),
                FullName = "System Administrator",
                Role = (int)Role.Administrator,
                BranchId = null,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
