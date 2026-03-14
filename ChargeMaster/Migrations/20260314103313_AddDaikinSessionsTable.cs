using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChargeMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddDaikinSessionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DaikinSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TargetTemperature = table.Column<double>(type: "double precision", nullable: false),
                    IsHeating = table.Column<bool>(type: "boolean", nullable: false),
                    ArbetsrumTemperature = table.Column<double>(type: "double precision", nullable: true),
                    HallTemperature = table.Column<double>(type: "double precision", nullable: true),
                    SovrumTemperature = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DaikinSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DaikinSessions_Timestamp",
                table: "DaikinSessions",
                column: "Timestamp",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DaikinSessions");
        }
    }
}
