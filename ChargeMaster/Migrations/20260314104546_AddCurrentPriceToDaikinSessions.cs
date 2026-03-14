using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentPriceToDaikinSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentPrice",
                table: "DaikinSessions",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPrice",
                table: "DaikinSessions");
        }
    }
}
