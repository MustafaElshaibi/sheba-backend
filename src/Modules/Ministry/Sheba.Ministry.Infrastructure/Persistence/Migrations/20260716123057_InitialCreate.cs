using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Ministry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ministry");

            migrationBuilder.CreateTable(
                name: "ministries",
                schema: "ministry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_ministry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description_ar = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    logo_url = table.Column<string>(type: "text", nullable: true),
                    website_url = table.Column<string>(type: "text", nullable: true),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_ar = table.Column<string>(type: "text", nullable: true),
                    address_en = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    depth_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ministries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ministry_auth_configs",
                schema: "ministry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ministry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    auth_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    health_check_path = table.Column<string>(type: "text", nullable: true),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ministry_auth_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_ministry_auth_configs_ministries_ministry_id",
                        column: x => x.ministry_id,
                        principalSchema: "ministry",
                        principalTable: "ministries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ministry_endpoints",
                schema: "ministry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ministry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auth_config_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description_ar = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    path_template = table.Column<string>(type: "text", nullable: false),
                    request_schema = table.Column<string>(type: "jsonb", nullable: true),
                    response_schema = table.Column<string>(type: "jsonb", nullable: true),
                    endpoint_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    rate_limit_per_minute = table.Column<int>(type: "integer", nullable: true),
                    requires_citizen_consent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ministry_endpoints", x => x.id);
                    table.ForeignKey(
                        name: "FK_ministry_endpoints_ministries_ministry_id",
                        column: x => x.ministry_id,
                        principalSchema: "ministry",
                        principalTable: "ministries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ministry_webhooks",
                schema: "ministry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ministry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sheba_webhook_path = table.Column<string>(type: "text", nullable: false),
                    signing_secret = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ministry_webhooks", x => x.id);
                    table.ForeignKey(
                        name: "FK_ministry_webhooks_ministries_ministry_id",
                        column: x => x.ministry_id,
                        principalSchema: "ministry",
                        principalTable: "ministries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ministry_auth_credentials",
                schema: "ministry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auth_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    oidc_token_endpoint = table.Column<string>(type: "text", nullable: true),
                    oidc_client_id = table.Column<string>(type: "text", nullable: true),
                    oidc_client_secret = table.Column<string>(type: "text", nullable: true),
                    oidc_scope = table.Column<string>(type: "text", nullable: true),
                    api_key_header_name = table.Column<string>(type: "text", nullable: true),
                    api_key_value = table.Column<string>(type: "text", nullable: true),
                    api_key_placement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bearer_token = table.Column<string>(type: "text", nullable: true),
                    basic_username = table.Column<string>(type: "text", nullable: true),
                    basic_password = table.Column<string>(type: "text", nullable: true),
                    cached_access_token = table.Column<string>(type: "text", nullable: true),
                    token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ministry_auth_credentials", x => x.id);
                    table.ForeignKey(
                        name: "FK_ministry_auth_credentials_ministry_auth_configs_auth_config~",
                        column: x => x.auth_config_id,
                        principalSchema: "ministry",
                        principalTable: "ministry_auth_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ministries_code",
                schema: "ministry",
                table: "ministries",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ministry_auth_configs_ministry_id",
                schema: "ministry",
                table: "ministry_auth_configs",
                column: "ministry_id");

            migrationBuilder.CreateIndex(
                name: "IX_ministry_auth_credentials_auth_config_id",
                schema: "ministry",
                table: "ministry_auth_credentials",
                column: "auth_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ministry_endpoints_ministry_code",
                schema: "ministry",
                table: "ministry_endpoints",
                columns: new[] { "ministry_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ministry_webhooks_ministry_id",
                schema: "ministry",
                table: "ministry_webhooks",
                column: "ministry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ministry_auth_credentials",
                schema: "ministry");

            migrationBuilder.DropTable(
                name: "ministry_endpoints",
                schema: "ministry");

            migrationBuilder.DropTable(
                name: "ministry_webhooks",
                schema: "ministry");

            migrationBuilder.DropTable(
                name: "ministry_auth_configs",
                schema: "ministry");

            migrationBuilder.DropTable(
                name: "ministries",
                schema: "ministry");
        }
    }
}
