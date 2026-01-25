using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<ElectricityPrice> ElectricityPrices { get; set; }
        public DbSet<WallboxMeterReading> WallboxMeterReadings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ensure decimal precision matches migrations for SQL Server
            modelBuilder.Entity<ElectricityPrice>(b =>
            {
                b.Property(e => e.SekPerKwh).HasColumnType("decimal(18,2)");
                b.Property(e => e.EurPerKwh).HasColumnType("decimal(18,2)");
                b.Property(e => e.ExchangeRate).HasColumnType("decimal(18,2)");
                b.Property(e => e.TimeStart)
                    .HasColumnType("timestamp without time zone")
                    .HasConversion(
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
                b.Property(e => e.TimeEnd)
                    .HasColumnType("timestamp without time zone")
                    .HasConversion(
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
            });
            modelBuilder.Entity<WallboxMeterReading>(b =>
            {
                b.Property(e => e.ReadAt)
                    .HasColumnType("timestamp without time zone")
                    .HasConversion(
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
            });
        }
    }
}
