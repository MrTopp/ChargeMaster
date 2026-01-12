using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChargeMaster.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Prefer environment variable, fallback to LocalDB for design-time
        var conn = Environment.GetEnvironmentVariable("CHARGEMASTER_CONNECTION")
                   ?? "Server=(localdb)\\MSSQLLocalDB;Database=ChargeMaster.Dev;Trusted_Connection=True;MultipleActiveResultSets=true";

        builder.UseSqlServer(conn);
        return new ApplicationDbContext(builder.Options);
    }
}
