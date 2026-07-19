using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFotosAviso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FotosAviso",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosAviso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotosAviso_Avisos_AvisoId",
                        column: x => x.AvisoId,
                        principalSchema: "catalog",
                        principalTable: "Avisos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosAviso_AvisoId",
                schema: "catalog",
                table: "FotosAviso",
                column: "AvisoId");

            migrationBuilder.CreateIndex(
                name: "IX_FotosAviso_AvisoId_Orden",
                schema: "catalog",
                table: "FotosAviso",
                columns: new[] { "AvisoId", "Orden" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosAviso",
                schema: "catalog");
        }
    }
}
