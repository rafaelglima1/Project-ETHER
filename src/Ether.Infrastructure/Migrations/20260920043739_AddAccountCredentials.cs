using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ether.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "accounts",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "accounts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ux_accounts_email",
                table: "accounts",
                column: "email",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_accounts_email_not_blank",
                table: "accounts",
                sql: "char_length(btrim(email)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_accounts_password_hash_not_blank",
                table: "accounts",
                sql: "char_length(btrim(password_hash)) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_accounts_email",
                table: "accounts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_accounts_email_not_blank",
                table: "accounts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_accounts_password_hash_not_blank",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "email",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "accounts");
        }
    }
}
