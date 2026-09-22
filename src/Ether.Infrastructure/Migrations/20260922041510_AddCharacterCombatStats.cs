using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ether.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterCombatStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "health",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "max_health",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_health_non_negative",
                table: "characters",
                sql: "health >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_health_within_max",
                table: "characters",
                sql: "health <= max_health");

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_max_health_positive",
                table: "characters",
                sql: "max_health >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_health_non_negative",
                table: "characters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_health_within_max",
                table: "characters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_max_health_positive",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "health",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "max_health",
                table: "characters");
        }
    }
}
