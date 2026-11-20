using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olvanta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchasingAndInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "Purchases",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Time = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Purchases_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Purchases_Masters_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warehouses_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Received = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseLines_Masters_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseLines_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalSchema: "inventory",
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Minimums",
                schema: "inventory",
                columns: table => new
                {
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Minimums", x => new { x.WarehouseId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_Minimums_Masters_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Minimums_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inventory",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Lot = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseLineId = table.Column<int>(type: "int", nullable: true),
                    RelatedId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false),
                    Time = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movements_Masters_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Movements_RelatedId",
                        column: x => x.RelatedId,
                        principalSchema: "inventory",
                        principalTable: "Movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_PurchaseLines_PurchaseLineId",
                        column: x => x.PurchaseLineId,
                        principalSchema: "inventory",
                        principalTable: "PurchaseLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inventory",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Minimums_ProductId",
                schema: "inventory",
                table: "Minimums",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_BranchId_OperationId_WarehouseId",
                schema: "inventory",
                table: "Movements",
                columns: new[] { "BranchId", "OperationId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movements_BranchId_ProductId_WarehouseId_Lot",
                schema: "inventory",
                table: "Movements",
                columns: new[] { "BranchId", "ProductId", "WarehouseId", "Lot" });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ProductId",
                schema: "inventory",
                table: "Movements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_PurchaseLineId",
                schema: "inventory",
                table: "Movements",
                column: "PurchaseLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_RelatedId",
                schema: "inventory",
                table: "Movements",
                column: "RelatedId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_WarehouseId",
                schema: "inventory",
                table: "Movements",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_ProductId",
                schema: "inventory",
                table: "PurchaseLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_PurchaseId",
                schema: "inventory",
                table: "PurchaseLines",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_BranchId_SupplierId_Reference",
                schema: "inventory",
                table: "Purchases",
                columns: new[] { "BranchId", "SupplierId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_SupplierId",
                schema: "inventory",
                table: "Purchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_BranchId_Name",
                schema: "inventory",
                table: "Warehouses",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.Sql("""
                IF USER_ID(N'olvanta_app') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT ON SCHEMA::[inventory] TO [olvanta_app];
                    GRANT UPDATE ON [inventory].[PurchaseLines] ([Received]) TO [olvanta_app];
                    GRANT UPDATE ON [inventory].[Minimums] ([Quantity]) TO [olvanta_app];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Minimums",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "PurchaseLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Warehouses",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Purchases",
                schema: "inventory");
        }
    }
}
