using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olvanta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "organization",
                table: "Branches",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "PEN");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                schema: "organization",
                table: "Branches",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Company",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Company", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Establishments",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Establishments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Establishments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "organization",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSeries",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EstablishmentId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentSeries_Establishments_EstablishmentId",
                        column: x => x.EstablishmentId,
                        principalSchema: "organization",
                        principalTable: "Establishments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSeries_DocumentType_Code",
                schema: "organization",
                table: "DocumentSeries",
                columns: new[] { "DocumentType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSeries_EstablishmentId",
                schema: "organization",
                table: "DocumentSeries",
                column: "EstablishmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Establishments_BranchId",
                schema: "organization",
                table: "Establishments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Establishments_Code",
                schema: "organization",
                table: "Establishments",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Company",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "DocumentSeries",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "Establishments",
                schema: "organization");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "organization",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                schema: "organization",
                table: "Branches");
        }
    }
}
