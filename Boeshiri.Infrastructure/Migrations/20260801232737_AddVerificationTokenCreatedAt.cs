using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boeshiri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationTokenCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "verification_tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            // Los tokens ya emitidos se fechan a partir de su caducidad (la vida del
            // token es de 24 h). Sin esto quedarían en el año 1, que es una fecha
            // falsa y confundiría a cualquiera que mire la tabla.
            migrationBuilder.Sql(
                "UPDATE verification_tokens SET created_at = expires_at - INTERVAL '24 hours';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "verification_tokens");
        }
    }
}
