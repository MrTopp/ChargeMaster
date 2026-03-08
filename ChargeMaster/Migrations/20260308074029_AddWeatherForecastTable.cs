using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChargeMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherForecastTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "ShellyTemperatures",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "ChargeSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SessionEnergy = table.Column<long>(type: "bigint", nullable: true),
                    SessionStartValue = table.Column<long>(type: "bigint", nullable: true),
                    SessionStartTime = table.Column<long>(type: "bigint", nullable: true),
                    ChargeLevel = table.Column<int>(type: "integer", nullable: true),
                    ChargeTarget = table.Column<int>(type: "integer", nullable: true),
                    ChargeState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeatherForecasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    CloudCoverage = table.Column<double>(type: "double precision", nullable: true),
                    Precipitation = table.Column<double>(type: "double precision", nullable: true),
                    WindSpeed = table.Column<double>(type: "double precision", nullable: true),
                    WindDirection = table.Column<int>(type: "integer", nullable: true),
                    Luftfuktighet = table.Column<int>(type: "integer", nullable: true),
                    Lufttryck = table.Column<double>(type: "double precision", nullable: true),
                    Sikt = table.Column<double>(type: "double precision", nullable: true),
                    MaxPrecipitation = table.Column<double>(type: "double precision", nullable: true),
                    MeanPrecipitation = table.Column<double>(type: "double precision", nullable: true),
                    WindGust = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherForecasts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargeSessions_SessionStartTime",
                table: "ChargeSessions",
                column: "SessionStartTime",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ChargeSessions_Timestamp",
                table: "ChargeSessions",
                column: "Timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_WeatherForecasts_Time",
                table: "WeatherForecasts",
                column: "Time",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargeSessions");

            migrationBuilder.DropTable(
                name: "WeatherForecasts");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "ShellyTemperatures",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
