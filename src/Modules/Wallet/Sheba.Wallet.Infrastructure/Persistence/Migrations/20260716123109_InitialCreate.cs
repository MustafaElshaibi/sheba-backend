using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Wallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wallet");

            migrationBuilder.CreateTable(
                name: "credential_schemas",
                schema: "wallet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_uri = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issuer_did = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schema_definition = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credential_schemas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "did_documents",
                schema: "wallet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    did = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    public_key_pem = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_did_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "verifiable_credentials",
                schema: "wallet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issuer_did = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_did = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    jwt = table.Column<string>(type: "text", nullable: false),
                    claims = table.Column<string>(type: "jsonb", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verifiable_credentials", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_did_documents_did",
                schema: "wallet",
                table: "did_documents",
                column: "did",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vc_subject",
                schema: "wallet",
                table: "verifiable_credentials",
                column: "subject_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credential_schemas",
                schema: "wallet");

            migrationBuilder.DropTable(
                name: "did_documents",
                schema: "wallet");

            migrationBuilder.DropTable(
                name: "verifiable_credentials",
                schema: "wallet");
        }
    }
}
