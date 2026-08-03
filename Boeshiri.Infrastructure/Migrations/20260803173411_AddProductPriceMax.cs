using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boeshiri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPriceMax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "price_max",
                table: "products",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "price_max",
                table: "products");
        }
    }
}
