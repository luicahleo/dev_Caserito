using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "Avisos",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PrecioMonto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioMoneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CiudadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Condicion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avisos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ciudades",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ciudades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Avisos_Estado",
                schema: "catalog",
                table: "Avisos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Avisos_VendedorId",
                schema: "catalog",
                table: "Avisos",
                column: "VendedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Avisos_VendedorId_Estado",
                schema: "catalog",
                table: "Avisos",
                columns: new[] { "VendedorId", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Avisos",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Categorias",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Ciudades",
                schema: "catalog");
        }
    }
}
