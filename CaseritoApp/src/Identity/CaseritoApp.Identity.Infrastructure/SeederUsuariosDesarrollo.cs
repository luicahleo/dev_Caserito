using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Configuración opt-in del seeder de identidades sintéticas de Development.</summary>
public sealed record OpcionesSeedUsuariosDesarrollo
{
    public const string Seccion = "SeedUsuariosDesarrollo";

    public bool Habilitado { get; init; }
    public string Password { get; init; } = string.Empty;
    public Dictionary<string, string> Correos { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Prepara y repara únicamente el conjunto determinista de usuarios sintéticos.</summary>
public sealed partial class SeederUsuariosDesarrollo(
    UserManager<ApplicationUser> usuarios,
    IdentityDbContext db,
    ILogger<SeederUsuariosDesarrollo> logger)
{
    public const string AliasAdminPlataforma = "admin-plataforma";
    public const string AliasRevisorKyc = "revisor-kyc";
    public const string AliasModerador = "moderador";
    public const string AliasSoporte = "soporte";
    public const string AliasVendedor1 = "vendedor-1";
    public const string AliasVendedor2 = "vendedor-2";
    public const string AliasComprador1 = "comprador-1";
    public const string AliasComprador2 = "comprador-2";
    public const string AliasEmailPendiente = "cliente-email-pendiente";
    public const string AliasKycNoIniciado = "cliente-kyc-no-iniciado";
    public const string AliasKycPendiente = "cliente-kyc-pendiente";
    public const string AliasKycRechazado = "cliente-kyc-rechazado";

    private static readonly Guid _revisorId = new("da7a0000-0000-4000-8000-000000000002");
    private static readonly Guid _ciudadCochabamba = new("22222222-2222-2222-2222-000000000001");

    private static readonly Persona[] _personas =
    [
        new(AliasAdminPlataforma, new("da7a0000-0000-4000-8000-000000000001"), RolesApp.AdminPlataforma, true, KycEsperado.NoIniciado),
        new(AliasRevisorKyc, _revisorId, RolesApp.AdminKyc, true, KycEsperado.NoIniciado),
        new(AliasModerador, new("da7a0000-0000-4000-8000-000000000003"), RolesApp.Moderador, true, KycEsperado.NoIniciado),
        new(AliasSoporte, new("da7a0000-0000-4000-8000-000000000004"), RolesApp.Soporte, true, KycEsperado.NoIniciado),
        new(AliasVendedor1, new("da7a0000-0000-4000-8000-000000000005"), RolesApp.Cliente, true, KycEsperado.Aprobada),
        new(AliasVendedor2, new("da7a0000-0000-4000-8000-000000000006"), RolesApp.Cliente, true, KycEsperado.Aprobada),
        new(AliasComprador1, new("da7a0000-0000-4000-8000-000000000007"), RolesApp.Cliente, true, KycEsperado.Aprobada),
        new(AliasComprador2, new("da7a0000-0000-4000-8000-000000000008"), RolesApp.Cliente, true, KycEsperado.Aprobada),
        new(AliasEmailPendiente, new("da7a0000-0000-4000-8000-000000000009"), RolesApp.Cliente, false, KycEsperado.NoIniciado),
        new(AliasKycNoIniciado, new("da7a0000-0000-4000-8000-00000000000a"), RolesApp.Cliente, true, KycEsperado.NoIniciado),
        new(AliasKycPendiente, new("da7a0000-0000-4000-8000-00000000000b"), RolesApp.Cliente, true, KycEsperado.Pendiente),
        new(AliasKycRechazado, new("da7a0000-0000-4000-8000-00000000000c"), RolesApp.Cliente, true, KycEsperado.Rechazada),
    ];

    public async Task EjecutarAsync(
        OpcionesSeedUsuariosDesarrollo opciones,
        bool esDevelopment,
        CancellationToken cancellationToken = default)
    {
        if (!esDevelopment || !opciones.Habilitado || !ConfiguracionCompleta(opciones))
        {
            SeedOmitido(logger);
            return;
        }

        var creados = 0;
        foreach (var persona in _personas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            creados += await PrepararAsync(persona, opciones.Correos[persona.Alias], opciones.Password, cancellationToken);
        }

        SeedCompletado(logger, creados, _personas.Length - creados);
    }

    private static bool ConfiguracionCompleta(OpcionesSeedUsuariosDesarrollo opciones) =>
        !string.IsNullOrWhiteSpace(opciones.Password)
        && _personas.All(p => opciones.Correos.TryGetValue(p.Alias, out var email) && !string.IsNullOrWhiteSpace(email));

    private async Task<int> PrepararAsync(Persona persona, string email, string password, CancellationToken ct)
    {
        var usuario = await usuarios.FindByIdAsync(persona.Id.ToString());
        var usuarioPorEmail = await usuarios.FindByEmailAsync(email);
        if (usuarioPorEmail is not null && usuarioPorEmail.Id != persona.Id)
        {
            throw ErrorGenerico();
        }

        var creado = 0;
        if (usuario is null)
        {
            usuario = new ApplicationUser
            {
                Id = persona.Id,
                UserName = email,
                Email = email,
                Nombres = NombreVisible(persona.Alias),
                Apellidos = "Sintético",
                CiudadId = _ciudadCochabamba,
                EmailConfirmed = persona.EmailConfirmado,
            };
            Exigir(await usuarios.CreateAsync(usuario, password));
            creado = 1;
        }
        else if (usuario.EmailConfirmed != persona.EmailConfirmado)
        {
            usuario.EmailConfirmed = persona.EmailConfirmado;
            Exigir(await usuarios.UpdateAsync(usuario));
        }

        await RepararRolAsync(usuario, persona.Rol);
        await RepararKycAsync(persona, ct);
        return creado;
    }

    private async Task RepararRolAsync(ApplicationUser usuario, string rolEsperado)
    {
        var actuales = await usuarios.GetRolesAsync(usuario);
        var sobrantes = actuales.Where(r => !string.Equals(r, rolEsperado, StringComparison.Ordinal)).ToArray();
        if (sobrantes.Length > 0)
        {
            Exigir(await usuarios.RemoveFromRolesAsync(usuario, sobrantes));
        }

        if (!actuales.Contains(rolEsperado, StringComparer.Ordinal))
        {
            Exigir(await usuarios.AddToRoleAsync(usuario, rolEsperado));
        }
    }

    private async Task RepararKycAsync(Persona persona, CancellationToken ct)
    {
        var verificacion = await db.VerificacionesKyc
            .Include(v => v.Solicitudes)
            .SingleOrDefaultAsync(v => v.Id == persona.Id, ct);

        if (persona.Kyc == KycEsperado.NoIniciado)
        {
            if (verificacion is not null && verificacion.Solicitudes.Count > 0)
            {
                throw ErrorGenerico();
            }

            return;
        }

        if (verificacion is null)
        {
            verificacion = VerificacionKyc.Crear(persona.Id);
            db.VerificacionesKyc.Add(verificacion);
        }

        var actual = verificacion.SolicitudActual;
        if (actual?.Estado == EstadoKyc.Aprobada && persona.Kyc != KycEsperado.Aprobada)
        {
            throw ErrorGenerico();
        }

        if (actual is null || actual.Estado == EstadoKyc.Rechazada)
        {
            if (persona.Kyc == KycEsperado.Rechazada && actual?.Estado == EstadoKyc.Rechazada)
            {
                return;
            }

            var enviada = verificacion.EnviarSolicitud(
                $"dev-kyc/{persona.Alias}/documento",
                $"dev-kyc/{persona.Alias}/selfie",
                TipoDocumento.CedulaIdentidad,
                DateTimeOffset.UnixEpoch);
            if (!enviada.EsExito)
            {
                throw ErrorGenerico();
            }

            actual = enviada.Valor;
        }

        if (persona.Kyc == KycEsperado.Aprobada && actual.Estado == EstadoKyc.Pendiente)
        {
            Exigir(verificacion.Aprobar(actual.Id, _revisorId, DateTimeOffset.UnixEpoch.AddMinutes(1)));
        }
        else if (persona.Kyc == KycEsperado.Rechazada && actual.Estado == EstadoKyc.Pendiente)
        {
            Exigir(verificacion.Rechazar(actual.Id, _revisorId, "Rechazo sintético de desarrollo.", DateTimeOffset.UnixEpoch.AddMinutes(1)));
        }

        await db.SaveChangesAsync(ct);
    }

    private static string NombreVisible(string alias) => alias switch
    {
        AliasAdminPlataforma => "Administración plataforma",
        AliasRevisorKyc => "Revisión KYC",
        AliasModerador => "Moderación",
        AliasSoporte => "Soporte",
        AliasVendedor1 or AliasVendedor2 => "Vendedor sintético",
        AliasComprador1 or AliasComprador2 => "Comprador sintético",
        _ => "Cliente sintético",
    };

    private static void Exigir(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            throw ErrorGenerico();
        }
    }

    private static void Exigir(CaseritoApp.BuildingBlocks.Domain.Result resultado)
    {
        if (!resultado.EsExito)
        {
            throw ErrorGenerico();
        }
    }

    private static InvalidOperationException ErrorGenerico() =>
        new("No se pudo preparar el entorno de desarrollo.");

    private sealed record Persona(string Alias, Guid Id, string Rol, bool EmailConfirmado, KycEsperado Kyc);
    private enum KycEsperado { NoIniciado, Pendiente, Aprobada, Rechazada }

    [LoggerMessage(EventId = 1100, Level = LogLevel.Debug, Message = "Seeder de usuarios de desarrollo omitido.")]
    private static partial void SeedOmitido(ILogger logger);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Seeder de usuarios de desarrollo completado. Nuevos: {Nuevos}; reparados o válidos: {Existentes}.")]
    private static partial void SeedCompletado(ILogger logger, int nuevos, int existentes);
}
