using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olvanta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommercialRentals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rentals");

            migrationBuilder.AddColumn<int>(
                name: "DepositLeaseId",
                schema: "commercial",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Units",
                schema: "rentals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Features = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Units_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                schema: "rentals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    PreviousId = table.Column<int>(type: "int", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    Tenant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conditions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    Deposit = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    DueDay = table.Column<int>(type: "int", nullable: false),
                    Start = table.Column<DateOnly>(type: "date", nullable: false),
                    End = table.Column<DateOnly>(type: "date", nullable: false),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Contracts_PreviousId",
                        column: x => x.PreviousId,
                        principalSchema: "rentals",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Masters_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Masters_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Units_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "rentals",
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                schema: "rentals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaseId = table.Column<int>(type: "int", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Through = table.Column<DateOnly>(type: "date", nullable: false),
                    Due = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Charges_Contracts_LeaseId",
                        column: x => x.LeaseId,
                        principalSchema: "rentals",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "commercial",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_DepositLeaseId",
                schema: "commercial",
                table: "Events",
                column: "DepositLeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_LeaseId_Kind_Reference",
                schema: "rentals",
                table: "Charges",
                columns: new[] { "LeaseId", "Kind", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charges_SaleId",
                schema: "rentals",
                table: "Charges",
                column: "SaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CustomerId",
                schema: "rentals",
                table: "Contracts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_PreviousId",
                schema: "rentals",
                table: "Contracts",
                column: "PreviousId",
                unique: true,
                filter: "[PreviousId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ServiceId",
                schema: "rentals",
                table: "Contracts",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_UnitId_Start_End",
                schema: "rentals",
                table: "Contracts",
                columns: new[] { "UnitId", "Start", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_Units_BranchId_Code",
                schema: "rentals",
                table: "Units",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Contracts_DepositLeaseId",
                schema: "commercial",
                table: "Events",
                column: "DepositLeaseId",
                principalSchema: "rentals",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                IF USER_ID(N'olvanta_app') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT ON SCHEMA::[rentals] TO [olvanta_app];
                    GRANT UPDATE ON [rentals].[Contracts] ([EndedOn], [EndReason]) TO [olvanta_app];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Contracts_DepositLeaseId",
                schema: "commercial",
                table: "Events");

            migrationBuilder.DropTable(
                name: "Charges",
                schema: "rentals");

            migrationBuilder.DropTable(
                name: "Contracts",
                schema: "rentals");

            migrationBuilder.DropTable(
                name: "Units",
                schema: "rentals");

            migrationBuilder.DropIndex(
                name: "IX_Events_DepositLeaseId",
                schema: "commercial",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "DepositLeaseId",
                schema: "commercial",
                table: "Events");
        }
    }
}
