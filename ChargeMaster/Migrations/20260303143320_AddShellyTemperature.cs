using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChargeMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddShellyTemperature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElectricityPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SekPerKwh = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EurPerKwh = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TimeStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TimeEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectricityPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShellyTemperatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    TemperatureCelsius = table.Column<double>(type: "double precision", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShellyTemperatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WallboxMeterReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RawJson = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AccEnergy = table.Column<long>(type: "bigint", nullable: false),
                    MeterSerial = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApparentPower = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WallboxMeterReadings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShellyTemperatures_DeviceId_Timestamp",
                table: "ShellyTemperatures",
                columns: new[] { "DeviceId", "Timestamp" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectricityPrices");

            migrationBuilder.DropTable(
                name: "ShellyTemperatures");

            migrationBuilder.DropTable(
                name: "WallboxMeterReadings");
        }
    }
}
