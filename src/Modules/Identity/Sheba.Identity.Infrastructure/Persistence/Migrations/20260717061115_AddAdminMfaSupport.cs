using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminMfaSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "mfa_enabled",
                schema: "identity",
                table: "admin_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "mfa_failed_attempts",
                schema: "identity",
                table: "admin_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "mfa_locked_until",
                schema: "identity",
                table: "admin_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "admin_recovery_codes",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "text", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_recovery_codes_admin_user_id",
                schema: "identity",
                table: "admin_recovery_codes",
                column: "admin_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_recovery_codes",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "mfa_enabled",
                schema: "identity",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "mfa_failed_attempts",
                schema: "identity",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "mfa_locked_until",
                schema: "identity",
                table: "admin_users");
        }
    }
}
