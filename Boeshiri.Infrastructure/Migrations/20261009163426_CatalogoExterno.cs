using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boeshiri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoExterno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_external_listing",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "products",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Los productos que ya existían: su último cambio conocido es la edición o el alta.
            migrationBuilder.Sql("UPDATE products SET updated_at = COALESCE(edited_at, created_at);");

            migrationBuilder.CreateTable(
                name: "catalog_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    credential_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    access_key_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    default_seller_id = table.Column<Guid>(type: "uuid", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sync_interval_minutes = table.Column<int>(type: "integer", nullable: true),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sync_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    last_sync_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    inbound_cursor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_connections", x => x.id);
                    table.ForeignKey(
                        name: "fk_catalog_connections_users_default_seller_id",
                        column: x => x.default_seller_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_external_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    remote_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_external_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_external_links_catalog_connections_connection_id",
                        column: x => x.connection_id,
                        principalTable: "catalog_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_external_links_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_products_updated_at",
                table: "products",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_connections_access_key_hash",
                table: "catalog_connections",
                column: "access_key_hash");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_connections_default_seller_id",
                table: "catalog_connections",
                column: "default_seller_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_external_links_connection_id_external_id",
                table: "product_external_links",
                columns: new[] { "connection_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_external_links_product_id_connection_id",
                table: "product_external_links",
                columns: new[] { "product_id", "connection_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_external_links");

            migrationBuilder.DropTable(
                name: "catalog_connections");

            migrationBuilder.DropIndex(
                name: "ix_products_updated_at",
                table: "products");

            migrationBuilder.DropColumn(
                name: "allow_external_listing",
                table: "products");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "products");
        }
    }
}
