using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olvanta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ElectronicBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Series = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileStem = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Ticket = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lease = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Snapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Digest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Xml = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Zip = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Cdr = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Documents_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "billing",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "commercial",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attempts",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    Time = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Response = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attempts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "billing",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SimulationReceipts",
                schema: "billing",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    Cdr = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationReceipts", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_SimulationReceipts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "billing",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_DocumentId",
                schema: "billing",
                table: "Attempts",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BranchId_RequestId",
                schema: "billing",
                table: "Documents",
                columns: new[] { "BranchId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Mode_SourceKey",
                schema: "billing",
                table: "Documents",
                columns: new[] { "Mode", "SourceKey" },
                unique: true,
                filter: "[Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Mode_Type_Series_Number",
                schema: "billing",
                table: "Documents",
                columns: new[] { "Mode", "Type", "Series", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ParentId",
                schema: "billing",
                table: "Documents",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SaleId",
                schema: "billing",
                table: "Documents",
                column: "SaleId");
            migrationBuilder.Sql("""
                IF USER_ID(N'olvanta_app') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT ON SCHEMA::[billing] TO [olvanta_app];
                    GRANT UPDATE ON [billing].[Documents] ([Status], [Active], [Ticket], [Cdr], [Lease], [LeaseUntil]) TO [olvanta_app];
                    GRANT UPDATE ON [billing].[Attempts] ([Outcome], [Detail], [Response]) TO [olvanta_app];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attempts",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "SimulationReceipts",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "billing");
        }
    }
}
