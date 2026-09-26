using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchemaWithBaselineAggregationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    industry = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    timezone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "activity_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    location = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    event_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_events_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_events_account_id_event_type_occurred_at",
                table: "activity_events",
                columns: new[] { "account_id", "event_type", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_events");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
