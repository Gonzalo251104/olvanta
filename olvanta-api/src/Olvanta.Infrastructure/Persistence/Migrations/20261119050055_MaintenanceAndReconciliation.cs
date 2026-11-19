using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olvanta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MaintenanceAndReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_CashSessionId",
                schema: "commercial",
                table: "Events");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextQuery",
                schema: "billing",
                table: "Documents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlannedQueries",
                schema: "billing",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDiscount",
                schema: "organization",
                table: "Branches",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.CreateIndex(
                name: "IX_Events_CashSessionId",
                schema: "commercial",
                table: "Events",
                column: "CashSessionId")
                .Annotation("SqlServer:Include", new[] { "CashDelta" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Mode_NextQuery",
                schema: "billing",
                table: "Documents",
                columns: new[] { "Mode", "NextQuery" },
                filter: "[NextQuery] IS NOT NULL");
            migrationBuilder.Sql("""
                IF USER_ID(N'olvanta_app') IS NOT NULL
                    GRANT UPDATE ON [billing].[Documents] ([NextQuery], [PlannedQueries]) TO [olvanta_app];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_CashSessionId",
                schema: "commercial",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Documents_Mode_NextQuery",
                schema: "billing",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "NextQuery",
                schema: "billing",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "PlannedQueries",
                schema: "billing",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MaxDiscount",
                schema: "organization",
                table: "Branches");

            migrationBuilder.CreateIndex(
                name: "IX_Events_CashSessionId",
                schema: "commercial",
                table: "Events",
                column: "CashSessionId");
        }
    }
}
