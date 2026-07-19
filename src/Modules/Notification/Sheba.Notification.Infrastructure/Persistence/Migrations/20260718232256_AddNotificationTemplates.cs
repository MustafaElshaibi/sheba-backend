using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_templates",
                schema: "notification",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_en = table.Column<string>(type: "text", nullable: false),
                    subject_ar = table.Column<string>(type: "text", nullable: false),
                    body_html_en = table.Column<string>(type: "text", nullable: false),
                    body_html_ar = table.Column<string>(type: "text", nullable: false),
                    body_text_en = table.Column<string>(type: "text", nullable: false),
                    body_text_ar = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_template_key",
                schema: "notification",
                table: "notification_templates",
                column: "template_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_templates",
                schema: "notification");
        }
    }
}
