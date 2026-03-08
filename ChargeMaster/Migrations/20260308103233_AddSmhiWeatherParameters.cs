using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddSmhiWeatherParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrecipitationCategory",
                table: "WeatherForecasts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PrecipitationMedian",
                table: "WeatherForecasts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrecipitationProbability",
                table: "WeatherForecasts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThunderstormProbability",
                table: "WeatherForecasts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TotalPrecipitation",
                table: "WeatherForecasts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeatherSymbol",
                table: "WeatherForecasts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecipitationCategory",
                table: "WeatherForecasts");

            migrationBuilder.DropColumn(
                name: "PrecipitationMedian",
                table: "WeatherForecasts");

            migrationBuilder.DropColumn(
                name: "PrecipitationProbability",
                table: "WeatherForecasts");

            migrationBuilder.DropColumn(
                name: "ThunderstormProbability",
                table: "WeatherForecasts");

            migrationBuilder.DropColumn(
                name: "TotalPrecipitation",
                table: "WeatherForecasts");

            migrationBuilder.DropColumn(
                name: "WeatherSymbol",
                table: "WeatherForecasts");
        }
    }
}
