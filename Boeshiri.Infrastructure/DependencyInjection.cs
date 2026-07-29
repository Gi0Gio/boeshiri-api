using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Admin;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Auth;
using Boeshiri.Application.Notifications;
using Boeshiri.Domain.Entities;
using Boeshiri.Infrastructure.Admin;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Auth;
using Boeshiri.Infrastructure.Documents;
using Boeshiri.Infrastructure.Finance;
using Boeshiri.Application.Documents;
using Boeshiri.Application.Events;
using Boeshiri.Application.Finance;
using Boeshiri.Application.Groups;
using Boeshiri.Application.Marketplace;
using Boeshiri.Application.Profiles;
using Boeshiri.Application.Publications;
using Boeshiri.Application.Transparency;
using Boeshiri.Infrastructure.Email;
using Boeshiri.Infrastructure.Events;
using Boeshiri.Infrastructure.Groups;
using Boeshiri.Infrastructure.Marketplace;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Profiles;
using Boeshiri.Infrastructure.Publications;
using Boeshiri.Infrastructure.Transparency;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boeshiri.Infrastructure;

/// <summary>
/// Registro de la capa de infraestructura: persistencia, autenticación, correo
/// (y más adelante storage). Se invoca desde Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // ── Persistencia ─────────────────────────────────────────
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default'.");

        services.AddDbContext<BoeshiriDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        // ── Opciones fuertemente tipadas ─────────────────────────
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AppOptions>()
            .Bind(config.GetSection(AppOptions.SectionName));

        // ── Autenticación / correo ───────────────────────────────
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        // ── Transversales: notificaciones y auditoría ────────────
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        // ── Administración ───────────────────────────────────────
        services.AddScoped<IPostulantesService, PostulantesService>();

        // ── Contenido ────────────────────────────────────────────
        services.AddScoped<IPublicationService, PublicationService>();

        // ── Grupos (comisiones, equipos, Kanban) ─────────────────
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IKanbanService, KanbanService>();

        // ── Eventos ──────────────────────────────────────────────
        services.AddScoped<IEventService, EventService>();

        // ── Perfil / Comunidad ───────────────────────────────────
        services.AddScoped<IProfileService, ProfileService>();

        // ── Marketplace ──────────────────────────────────────────
        services.AddScoped<IMarketplaceService, MarketplaceService>();

        // ── Documentos / Biblioteca ──────────────────────────────
        services.AddScoped<IDocumentService, DocumentService>();

        // ── Finanzas ─────────────────────────────────────────────
        services.AddScoped<IFinanceService, FinanceService>();

        // ── Transparencia ────────────────────────────────────────
        services.AddScoped<ITransparencyService, TransparencyService>();

        return services;
    }
}
