using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_ID_1_AccountLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deactivation_reason",
                schema: "identity",
                table: "accounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "identity",
                table: "accounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suspension_reason",
                schema: "identity",
                table: "accounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deactivation_reason",
                schema: "identity",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "identity",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "suspension_reason",
                schema: "identity",
                table: "accounts");
        }
    }
}
