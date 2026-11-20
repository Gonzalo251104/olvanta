using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olvanta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ParkingOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "parking");

            migrationBuilder.CreateTable(
                name: "Sites",
                schema: "parking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sites_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Passes",
                schema: "parking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Through = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Passes_Events_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "commercial",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Passes_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "parking",
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rates",
                schema: "parking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    BlockMinutes = table.Column<int>(type: "int", nullable: false),
                    GraceMinutes = table.Column<int>(type: "int", nullable: false),
                    BlockPrice = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rates_Masters_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rates_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "parking",
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zones",
                schema: "parking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zones_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "parking",
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Stays",
                schema: "parking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    ZoneId = table.Column<int>(type: "int", nullable: false),
                    RateId = table.Column<int>(type: "int", nullable: false),
                    PassId = table.Column<int>(type: "int", nullable: true),
                    Plate = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    EntryKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Entered = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Exited = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExitKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExitHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Blocks = table.Column<int>(type: "int", nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stays_Events_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "commercial",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stays_Passes_PassId",
                        column: x => x.PassId,
                        principalSchema: "parking",
                        principalTable: "Passes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stays_Rates_RateId",
                        column: x => x.RateId,
                        principalSchema: "parking",
                        principalTable: "Rates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stays_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "parking",
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stays_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalSchema: "parking",
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Passes_PaymentId",
                schema: "parking",
                table: "Passes",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Passes_SiteId_Plate_From",
                schema: "parking",
                table: "Passes",
                columns: new[] { "SiteId", "Plate", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_Passes_SiteId_RequestId",
                schema: "parking",
                table: "Passes",
                columns: new[] { "SiteId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rates_ServiceId",
                schema: "parking",
                table: "Rates",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Rates_SiteId",
                schema: "parking",
                table: "Rates",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_BranchId_Name",
                schema: "parking",
                table: "Sites",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stays_PassId",
                schema: "parking",
                table: "Stays",
                column: "PassId");

            migrationBuilder.CreateIndex(
                name: "IX_Stays_PaymentId",
                schema: "parking",
                table: "Stays",
                column: "PaymentId",
                unique: true,
                filter: "[PaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stays_RateId",
                schema: "parking",
                table: "Stays",
                column: "RateId");

            migrationBuilder.CreateIndex(
                name: "IX_Stays_SiteId_EntryKey",
                schema: "parking",
                table: "Stays",
                columns: new[] { "SiteId", "EntryKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stays_SiteId_ExitKey",
                schema: "parking",
                table: "Stays",
                columns: new[] { "SiteId", "ExitKey" },
                unique: true,
                filter: "[ExitKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stays_SiteId_Plate",
                schema: "parking",
                table: "Stays",
                columns: new[] { "SiteId", "Plate" },
                unique: true,
                filter: "[Exited] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stays_ZoneId",
                schema: "parking",
                table: "Stays",
                column: "ZoneId",
                filter: "[Exited] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_SiteId_Name",
                schema: "parking",
                table: "Zones",
                columns: new[] { "SiteId", "Name" },
                unique: true);
            migrationBuilder.Sql("""
                IF USER_ID(N'olvanta_app') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT ON SCHEMA::[parking] TO [olvanta_app];
                    GRANT UPDATE ON [parking].[Stays] ([Exited], [ExitKey], [ExitHash], [Blocks], [PaymentId]) TO [olvanta_app];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stays",
                schema: "parking");

            migrationBuilder.DropTable(
                name: "Passes",
                schema: "parking");

            migrationBuilder.DropTable(
                name: "Rates",
                schema: "parking");

            migrationBuilder.DropTable(
                name: "Zones",
                schema: "parking");

            migrationBuilder.DropTable(
                name: "Sites",
                schema: "parking");
        }
    }
}
