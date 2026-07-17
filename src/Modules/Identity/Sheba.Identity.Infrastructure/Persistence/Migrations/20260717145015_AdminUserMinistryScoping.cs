using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminUserMinistryScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ministry_id",
                schema: "identity",
                table: "admin_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_ministry_id",
                schema: "identity",
                table: "admin_users",
                column: "ministry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_admin_users_ministry_id",
                schema: "identity",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "ministry_id",
                schema: "identity",
                table: "admin_users");
        }
    }
}
