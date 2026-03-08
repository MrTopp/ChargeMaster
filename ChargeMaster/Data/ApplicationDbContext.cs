using Microsoft.EntityFrameworkCore;
using ChargeMaster.Services.SMHI;

namespace ChargeMaster.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<ElectricityPrice> ElectricityPrices { get; set; }
            public DbSet<WallboxMeterReading> WallboxMeterReadings { get; set; }
            public DbSet<ShellyTemperature> ShellyTemperatures { get; set; }
            public DbSet<ChargeSession> ChargeSessions { get; set; }
            public DbSet<WeatherForecast> WeatherForecasts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Säkerställ decimalprecision matchar migrations
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
            modelBuilder.Entity<ShellyTemperature>(b =>
            {
                b.Property(e => e.Timestamp)
                    .HasColumnType("timestamp without time zone")
                    .HasConversion(
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
                b.HasIndex(e => new { e.DeviceId, e.Timestamp }).IsDescending(false, true);
            });
            modelBuilder.Entity<ChargeSession>(b =>
            {
                b.Property(e => e.Timestamp)
                    .HasColumnType("timestamp without time zone")
                    .HasConversion(
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
                b.HasIndex(e => e.SessionStartTime).IsDescending();
                b.HasIndex(e => e.Timestamp).IsDescending();
            });
            modelBuilder.Entity<WeatherForecast>(b =>
            {
                b.Property(e => e.Time)
                    .HasColumnType("timestamp without time zone")
                    .HasConversion(
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
                b.HasIndex(e => e.Time).IsUnique();
            });
        }
    }
}
