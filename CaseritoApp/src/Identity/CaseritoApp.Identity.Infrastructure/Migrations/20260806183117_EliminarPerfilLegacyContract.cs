using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EliminarPerfilLegacyContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Preservar datos antes del contract: copiar Nombre (legacy) a Nombres donde falte.
                IF COL_LENGTH(N'[identity].[AspNetUsers]', N'Nombre') IS NOT NULL
                BEGIN
                    UPDATE [identity].[AspNetUsers]
                    SET [Nombres] = [Nombre]
                    WHERE ([Nombres] IS NULL OR [Nombres] = N'')
                      AND [Nombre] IS NOT NULL AND [Nombre] <> N'';
                END

                -- Mapear Ciudad (texto legacy) a CiudadId por nombre exacto del catálogo.
                -- Solo si el catálogo convive en esta base (no aplica en bases de tests por contexto).
                IF COL_LENGTH(N'[identity].[AspNetUsers]', N'Ciudad') IS NOT NULL
                   AND OBJECT_ID(N'[catalog].[Ciudades]', N'U') IS NOT NULL
                BEGIN
                    UPDATE u
                    SET u.[CiudadId] = c.[Id]
                    FROM [identity].[AspNetUsers] u
                    INNER JOIN [catalog].[Ciudades] c ON c.[Nombre] = u.[Ciudad]
                    WHERE u.[CiudadId] = '00000000-0000-0000-0000-000000000000'
                      AND u.[Ciudad] IS NOT NULL AND u.[Ciudad] <> N'';
                END

                IF COL_LENGTH(N'[identity].[AspNetUsers]', N'Nombre') IS NOT NULL
                    ALTER TABLE [identity].[AspNetUsers] DROP COLUMN [Nombre];

                IF COL_LENGTH(N'[identity].[AspNetUsers]', N'Ciudad') IS NOT NULL
                    ALTER TABLE [identity].[AspNetUsers] DROP COLUMN [Ciudad];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Restaura las columnas vacías (los datos legacy no se recuperan).
                IF COL_LENGTH(N'[identity].[AspNetUsers]', N'Nombre') IS NULL
                    ALTER TABLE [identity].[AspNetUsers] ADD [Nombre] nvarchar(max) NULL;

                IF COL_LENGTH(N'[identity].[AspNetUsers]', N'Ciudad') IS NULL
                    ALTER TABLE [identity].[AspNetUsers] ADD [Ciudad] nvarchar(max) NULL;
                """);
        }
    }
}
