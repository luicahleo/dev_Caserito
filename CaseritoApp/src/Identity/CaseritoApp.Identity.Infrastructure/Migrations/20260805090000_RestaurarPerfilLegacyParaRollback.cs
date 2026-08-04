using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Identity.Infrastructure.Migrations
{
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260805090000_RestaurarPerfilLegacyParaRollback")]
    public partial class RestaurarPerfilLegacyParaRollback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'[identity].[AspNetUsers]', N'Ciudad') IS NULL
                    ALTER TABLE [identity].[AspNetUsers] ADD [Ciudad] nvarchar(max) NULL;
                ELSE
                    ALTER TABLE [identity].[AspNetUsers] ALTER COLUMN [Ciudad] nvarchar(max) NULL;

                IF COL_LENGTH(N'[identity].[AspNetUsers]', N'Nombre') IS NULL
                    ALTER TABLE [identity].[AspNetUsers] ADD [Nombre] nvarchar(max) NULL;
                ELSE
                    ALTER TABLE [identity].[AspNetUsers] ALTER COLUMN [Nombre] nvarchar(max) NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se preservan las columnas porque una imagen anterior todavía puede necesitarlas.
        }
    }
}
