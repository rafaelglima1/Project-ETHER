using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ether.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "item_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    owner_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_type = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_instances", x => x.id);
                    table.CheckConstraint("ck_item_instances_quantity_positive", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_item_instances_characters_owner_character_id",
                        column: x => x.owner_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_instances_owner_character_id",
                table: "item_instances",
                column: "owner_character_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_instances");
        }
    }
}
