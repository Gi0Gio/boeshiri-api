using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boeshiri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AutoresSinCascada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_documents_users_author_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "fk_transparency_articles_users_author_id",
                table: "transparency_articles");

            migrationBuilder.AddForeignKey(
                name: "fk_documents_users_author_id",
                table: "documents",
                column: "author_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transparency_articles_users_author_id",
                table: "transparency_articles",
                column: "author_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_documents_users_author_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "fk_transparency_articles_users_author_id",
                table: "transparency_articles");

            migrationBuilder.AddForeignKey(
                name: "fk_documents_users_author_id",
                table: "documents",
                column: "author_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_transparency_articles_users_author_id",
                table: "transparency_articles",
                column: "author_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
