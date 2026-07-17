using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "service_req");

            migrationBuilder.CreateTable(
                name: "service_categories",
                schema: "service_req",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icon_url = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_requests",
                schema: "service_req",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    citizen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    current_step = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    form_data = table.Column<string>(type: "jsonb", nullable: true),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "NORMAL"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "services",
                schema: "service_req",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ministry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description_ar = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    eligibility_rules = table.Column<string>(type: "jsonb", nullable: true),
                    required_loa = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    requires_appointment = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_online = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    average_days = table.Column<int>(type: "integer", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tags = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_services", x => x.id);
                    table.ForeignKey(
                        name: "FK_services_service_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "service_req",
                        principalTable: "service_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_step_executions",
                schema: "service_req",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_step_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_request_step_executions_service_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "service_req",
                        principalTable: "service_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_fees",
                schema: "service_req",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "YER"),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_fees", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_fees_services_service_id",
                        column: x => x.service_id,
                        principalSchema: "service_req",
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_forms",
                schema: "service_req",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "1.0"),
                    form_schema = table.Column<string>(type: "jsonb", nullable: false),
                    ui_schema = table.Column<string>(type: "jsonb", nullable: true),
                    validation_rules = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_forms", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_forms_services_service_id",
                        column: x => x.service_id,
                        principalSchema: "service_req",
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_required_documents",
                schema: "service_req",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    max_size_mb = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    allowed_mime_types = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_required_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_required_documents_services_service_id",
                        column: x => x.service_id,
                        principalSchema: "service_req",
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_workflow_steps",
                schema: "service_req",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    step_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    actor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ministry_endpoint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    timeout_hours = table.Column<int>(type: "integer", nullable: true),
                    is_automated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    on_success_step = table.Column<int>(type: "integer", nullable: true),
                    on_failure_step = table.Column<int>(type: "integer", nullable: true),
                    config = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_workflow_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_workflow_steps_services_service_id",
                        column: x => x.service_id,
                        principalSchema: "service_req",
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_request_step_executions_request_id",
                schema: "service_req",
                table: "request_step_executions",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_fees_service_id",
                schema: "service_req",
                table: "service_fees",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_forms_service_id",
                schema: "service_req",
                table: "service_forms",
                column: "service_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_requests_citizen",
                schema: "service_req",
                table: "service_requests",
                column: "citizen_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_requests_ref",
                schema: "service_req",
                table: "service_requests",
                column: "reference_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_required_documents_service_id",
                schema: "service_req",
                table: "service_required_documents",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_workflow_steps_service_id",
                schema: "service_req",
                table: "service_workflow_steps",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_services_category_id",
                schema: "service_req",
                table: "services",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_services_code",
                schema: "service_req",
                table: "services",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_step_executions",
                schema: "service_req");

            migrationBuilder.DropTable(
                name: "service_fees",
                schema: "service_req");

            migrationBuilder.DropTable(
                name: "service_forms",
                schema: "service_req");

            migrationBuilder.DropTable(
                name: "service_required_documents",
                schema: "service_req");

            migrationBuilder.DropTable(
                name: "service_workflow_steps",
                schema: "service_req");

            migrationBuilder.DropTable(
                name: "service_requests",
                schema: "service_req");

            migrationBuilder.DropTable(
                name: "services",
                schema: "service_req");

            migrationBuilder.DropTable(
                name: "service_categories",
                schema: "service_req");
        }
    }
}
