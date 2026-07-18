using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Ministry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MinistryHealthColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_health_check_at",
                schema: "ministry",
                table: "ministry_auth_configs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_health_error",
                schema: "ministry",
                table: "ministry_auth_configs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "last_health_latency_ms",
                schema: "ministry",
                table: "ministry_auth_configs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "last_health_success",
                schema: "ministry",
                table: "ministry_auth_configs",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_health_check_at",
                schema: "ministry",
                table: "ministry_auth_configs");

            migrationBuilder.DropColumn(
                name: "last_health_error",
                schema: "ministry",
                table: "ministry_auth_configs");

            migrationBuilder.DropColumn(
                name: "last_health_latency_ms",
                schema: "ministry",
                table: "ministry_auth_configs");

            migrationBuilder.DropColumn(
                name: "last_health_success",
                schema: "ministry",
                table: "ministry_auth_configs");
        }
    }
}
