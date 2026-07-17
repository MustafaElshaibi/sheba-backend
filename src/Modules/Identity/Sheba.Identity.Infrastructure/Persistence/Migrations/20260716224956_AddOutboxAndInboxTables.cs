using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxAndInboxTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_unpublished",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "error",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.RenameColumn(
                name: "retry_count",
                schema: "identity",
                table: "outbox_messages",
                newName: "status");

            migrationBuilder.AlterColumn<string>(
                name: "event_type",
                schema: "identity",
                table: "outbox_messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "aggregate_type",
                schema: "identity",
                table: "outbox_messages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "attempts",
                schema: "identity",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                schema: "identity",
                table: "outbox_messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                schema: "identity",
                table: "outbox_messages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_attempt_at",
                schema: "identity",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt",
                schema: "identity",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ux_inbox_messages_event_consumer",
                schema: "identity",
                table: "inbox_messages",
                columns: new[] { "event_id", "consumer_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_status_next_attempt",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "attempts",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "event_id",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "last_error",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                schema: "identity",
                table: "outbox_messages");

            migrationBuilder.RenameColumn(
                name: "status",
                schema: "identity",
                table: "outbox_messages",
                newName: "retry_count");

            migrationBuilder.AlterColumn<string>(
                name: "event_type",
                schema: "identity",
                table: "outbox_messages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "aggregate_type",
                schema: "identity",
                table: "outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "error",
                schema: "identity",
                table: "outbox_messages",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unpublished",
                schema: "identity",
                table: "outbox_messages",
                column: "published_at",
                filter: "published_at IS NULL");
        }
    }
}
