using FinalProjectFleetLink.Data;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Helpers;

public static class DbContextFactory
{
    public static FleetLinkDbContext Create()
    {
        var options = new DbContextOptionsBuilder<FleetLinkDbContext>()
            .UseSqlServer(new DbConnections().ConnectionString)
            .Options;

        return new FleetLinkDbContext(options);
    }
}
