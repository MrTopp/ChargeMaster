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
                   ?? "Server=THOMASPC\\SQL2022;Database=ChargeMasterTest;Trusted_Connection=True;TrustServerCertificate=True;";

        builder.UseSqlServer(conn);
        return new ApplicationDbContext(builder.Options);
    }
}
