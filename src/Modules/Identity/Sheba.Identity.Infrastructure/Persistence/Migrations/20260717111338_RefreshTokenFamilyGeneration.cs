using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sheba.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenFamilyGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_token_hash",
                schema: "identity",
                table: "refresh_token_families");

            migrationBuilder.RenameColumn(
                name: "account_id",
                schema: "identity",
                table: "refresh_token_families",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_refresh_token_families_account_id",
                schema: "identity",
                table: "refresh_token_families",
                newName: "ix_refresh_token_families_subject_id");

            migrationBuilder.AddColumn<int>(
                name: "generation",
                schema: "identity",
                table: "refresh_token_families",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "generation",
                schema: "identity",
                table: "refresh_token_families");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                schema: "identity",
                table: "refresh_token_families",
                newName: "account_id");

            migrationBuilder.RenameIndex(
                name: "ix_refresh_token_families_subject_id",
                schema: "identity",
                table: "refresh_token_families",
                newName: "ix_refresh_token_families_account_id");

            migrationBuilder.AddColumn<string>(
                name: "current_token_hash",
                schema: "identity",
                table: "refresh_token_families",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
