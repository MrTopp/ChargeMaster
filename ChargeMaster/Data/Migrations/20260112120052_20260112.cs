using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeMaster.Migrations
{
    public partial class _20260112 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add WallboxMeterReadings table
            migrationBuilder.CreateTable(
                name: "WallboxMeterReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccEnergy = table.Column<double>(type: "float", nullable: false),
                    MeterSerial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApparentPower = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WallboxMeterReadings", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WallboxMeterReadings");
        }
    }
}
