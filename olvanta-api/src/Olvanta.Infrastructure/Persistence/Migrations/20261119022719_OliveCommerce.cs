using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olvanta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OliveCommerce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "olives");

            migrationBuilder.CreateTable(
                name: "Lots",
                schema: "olives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Product = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Presentation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KgPerUnit = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    WholeUnits = table.Column<bool>(type: "bit", nullable: false),
                    RootId = table.Column<int>(type: "int", nullable: true),
                    ProcessId = table.Column<int>(type: "int", nullable: true),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    Variety = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Origin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierLot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Supplier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gross = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Tare = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lots_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lots_Lots_RootId",
                        column: x => x.RootId,
                        principalSchema: "olives",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lots_Masters_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lots_Movements_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "inventory",
                        principalTable: "Movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Repacking",
                schema: "olives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    ConsumeId = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InputKg = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    LossKg = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    LossValue = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repacking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repacking_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Repacking_Lots_SourceId",
                        column: x => x.SourceId,
                        principalSchema: "olives",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Repacking_Movements_ConsumeId",
                        column: x => x.ConsumeId,
                        principalSchema: "inventory",
                        principalTable: "Movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lots_BranchId_ProductId_Code",
                schema: "olives",
                table: "Lots",
                columns: new[] { "BranchId", "ProductId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lots_ProcessId",
                schema: "olives",
                table: "Lots",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_ProductId",
                schema: "olives",
                table: "Lots",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_ReceiptId",
                schema: "olives",
                table: "Lots",
                column: "ReceiptId",
                unique: true,
                filter: "[ReceiptId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_RootId",
                schema: "olives",
                table: "Lots",
                column: "RootId");

            migrationBuilder.CreateIndex(
                name: "IX_Repacking_BranchId_OperationId",
                schema: "olives",
                table: "Repacking",
                columns: new[] { "BranchId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repacking_ConsumeId",
                schema: "olives",
                table: "Repacking",
                column: "ConsumeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repacking_SourceId",
                schema: "olives",
                table: "Repacking",
                column: "SourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lots_Repacking_ProcessId",
                schema: "olives",
                table: "Lots",
                column: "ProcessId",
                principalSchema: "olives",
                principalTable: "Repacking",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                IF USER_ID(N'olvanta_app') IS NOT NULL
                    GRANT SELECT, INSERT ON SCHEMA::[olives] TO [olvanta_app];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lots_Repacking_ProcessId",
                schema: "olives",
                table: "Lots");

            migrationBuilder.DropTable(
                name: "Repacking",
                schema: "olives");

            migrationBuilder.DropTable(
                name: "Lots",
                schema: "olives");
        }
    }
}
