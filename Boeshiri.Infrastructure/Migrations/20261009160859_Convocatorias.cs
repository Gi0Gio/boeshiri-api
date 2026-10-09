using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boeshiri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Convocatorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "open_calls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closes_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_open_calls", x => x.id);
                    table.ForeignKey(
                        name: "fk_open_calls_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "open_call_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    open_call_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    help = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    required = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_open_call_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_open_call_questions_open_calls_open_call_id",
                        column: x => x.open_call_id,
                        principalTable: "open_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "open_call_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    open_call_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_open_call_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_open_call_responses_open_calls_open_call_id",
                        column: x => x.open_call_id,
                        principalTable: "open_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "open_call_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_open_call_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_open_call_answers_open_call_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "open_call_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_open_call_answers_open_call_responses_response_id",
                        column: x => x.response_id,
                        principalTable: "open_call_responses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_open_call_answers_question_id",
                table: "open_call_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_open_call_answers_response_id",
                table: "open_call_answers",
                column: "response_id");

            migrationBuilder.CreateIndex(
                name: "ix_open_call_questions_open_call_id",
                table: "open_call_questions",
                column: "open_call_id");

            migrationBuilder.CreateIndex(
                name: "ix_open_call_responses_open_call_id_email",
                table: "open_call_responses",
                columns: new[] { "open_call_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_open_calls_event_id",
                table: "open_calls",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_open_calls_status",
                table: "open_calls",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "open_call_answers");

            migrationBuilder.DropTable(
                name: "open_call_questions");

            migrationBuilder.DropTable(
                name: "open_call_responses");

            migrationBuilder.DropTable(
                name: "open_calls");
        }
    }
}
