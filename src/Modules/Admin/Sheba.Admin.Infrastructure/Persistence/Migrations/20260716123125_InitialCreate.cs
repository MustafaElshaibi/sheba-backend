using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Admin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "admin_data");

            migrationBuilder.CreateTable(
                name: "analytics_identity_daily",
                schema: "admin_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_registrations = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    approved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rejected = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    pending_eod = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    avg_approval_hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analytics_identity_daily", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_service_requests_daily",
                schema: "admin_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ministry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    completed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rejected = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cancelled = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sla_breached = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    avg_completion_hours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analytics_service_requests_daily", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_jobs",
                schema: "admin_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    filters = table.Column<string>(type: "jsonb", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    hangfire_job_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_bytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    row_count = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analytics_identity_daily_date",
                schema: "admin_data",
                table: "analytics_identity_daily",
                column: "date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analytics_sr_daily_date_service",
                schema: "admin_data",
                table: "analytics_service_requests_daily",
                columns: new[] { "date", "service_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_identity_daily",
                schema: "admin_data");

            migrationBuilder.DropTable(
                name: "analytics_service_requests_daily",
                schema: "admin_data");

            migrationBuilder.DropTable(
                name: "report_jobs",
                schema: "admin_data");
        }
    }
}
