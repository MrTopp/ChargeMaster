using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeMaster.Migrations
{
    public partial class ConfigureDecimalTypes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration intentionally left empty; decimal types are configured in DbContext.OnModelCreating.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
