using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boeshiri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    place = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    happens_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    slots = table.Column<int>(type: "integer", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shouts", x => x.id);
                    table.ForeignKey(
                        name: "fk_shouts_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shout_joins",
                columns: table => new
                {
                    shout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shout_joins", x => new { x.shout_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_shout_joins_shouts_shout_id",
                        column: x => x.shout_id,
                        principalTable: "shouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shout_joins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shout_joins_user_id",
                table: "shout_joins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_shouts_author_id",
                table: "shouts",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_shouts_status_happens_at",
                table: "shouts",
                columns: new[] { "status", "happens_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shout_joins");

            migrationBuilder.DropTable(
                name: "shouts");
        }
    }
}
