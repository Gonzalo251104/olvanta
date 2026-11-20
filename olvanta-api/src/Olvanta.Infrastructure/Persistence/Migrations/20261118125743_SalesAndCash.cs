using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olvanta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesAndCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "commercial");

            migrationBuilder.AddColumn<int>(
                name: "SaleLineId",
                schema: "inventory",
                table: "Movements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashSessions",
                schema: "commercial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Opened = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Closed = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Opening = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    Counted = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashSessions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                schema: "commercial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Customer = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sales_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_Masters_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleLines",
                schema: "commercial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    Lot = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleLines_Masters_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleLines_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "commercial",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleLines_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inventory",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "commercial",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: true),
                    LineId = table.Column<int>(type: "int", nullable: true),
                    CashSessionId = table.Column<int>(type: "int", nullable: true),
                    ReturnId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CashDelta = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    Time = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "commercial",
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Events_ReturnId",
                        column: x => x.ReturnId,
                        principalSchema: "commercial",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_SaleLines_LineId",
                        column: x => x.LineId,
                        principalSchema: "commercial",
                        principalTable: "SaleLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "commercial",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_SaleLineId",
                schema: "inventory",
                table: "Movements",
                column: "SaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_BranchId",
                schema: "commercial",
                table: "CashSessions",
                column: "BranchId",
                unique: true,
                filter: "[Closed] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Events_BranchId_RequestId",
                schema: "commercial",
                table: "Events",
                columns: new[] { "BranchId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_CashSessionId",
                schema: "commercial",
                table: "Events",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_LineId",
                schema: "commercial",
                table: "Events",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ReturnId",
                schema: "commercial",
                table: "Events",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_SaleId",
                schema: "commercial",
                table: "Events",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_ItemId",
                schema: "commercial",
                table: "SaleLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_SaleId",
                schema: "commercial",
                table: "SaleLines",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_WarehouseId",
                schema: "commercial",
                table: "SaleLines",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchId",
                schema: "commercial",
                table: "Sales",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                schema: "commercial",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_SaleLines_SaleLineId",
                schema: "inventory",
                table: "Movements",
                column: "SaleLineId",
                principalSchema: "commercial",
                principalTable: "SaleLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                IF USER_ID(N'olvanta_app') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT ON SCHEMA::[commercial] TO [olvanta_app];
                    GRANT UPDATE ON [commercial].[Sales] ([Stage]) TO [olvanta_app];
                    GRANT UPDATE ON [commercial].[CashSessions] ([Closed], [Counted]) TO [olvanta_app];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movements_SaleLines_SaleLineId",
                schema: "inventory",
                table: "Movements");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "commercial");

            migrationBuilder.DropTable(
                name: "CashSessions",
                schema: "commercial");

            migrationBuilder.DropTable(
                name: "SaleLines",
                schema: "commercial");

            migrationBuilder.DropTable(
                name: "Sales",
                schema: "commercial");

            migrationBuilder.DropIndex(
                name: "IX_Movements_SaleLineId",
                schema: "inventory",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "SaleLineId",
                schema: "inventory",
                table: "Movements");
        }
    }
}
