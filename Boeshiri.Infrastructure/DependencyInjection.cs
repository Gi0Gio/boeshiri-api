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
using Boeshiri.Infrastructure.Sharing;
using Boeshiri.Infrastructure.Storage;
using Boeshiri.Infrastructure.Transparency;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Resend;

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

        // ── Almacenamiento de archivos (Cloudflare R2, opcional) ──
        services.AddOptions<R2Options>()
            .Bind(config.GetSection(R2Options.SectionName));
        var r2 = config.GetSection(R2Options.SectionName).Get<R2Options>() ?? new R2Options();
        if (r2.IsConfigured)
            services.AddSingleton<IFileStorage, R2FileStorage>();
        else
            services.AddSingleton<IFileStorage, DisabledFileStorage>();

        // Valida y normaliza lo que se sube (WebP, límites, lista blanca) antes de
        // que llegue al bucket. Se registra siempre, aunque R2 esté deshabilitado.
        services.AddSingleton<IUploadProcessor, UploadProcessor>();
        services.AddScoped<IFileManagerService, FileManagerService>();

        // Tarjetas para compartir en redes. Lleva HttpClient porque descarga la
        // imagen del anuncio o la publicación para componerla dentro.
        services.AddHttpClient<IShareCardRenderer, ShareCardRenderer>();

        // ── Autenticación / correo ───────────────────────────────
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();

        // Correo: Resend si hay API key; si no, el emisor de desarrollo que solo
        // escribe el mensaje (con su enlace) en el log (ADR-0003).
        services.AddOptions<ResendOptions>()
            .Bind(config.GetSection(ResendOptions.SectionName));
        var resend = config.GetSection(ResendOptions.SectionName).Get<ResendOptions>() ?? new ResendOptions();
        if (resend.IsConfigured)
        {
            services.AddResend(o => o.ApiToken = resend.ApiKey);
            services.AddScoped<IEmailSender, ResendEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }

        // ── Transversales: notificaciones y auditoría ────────────
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        // ── Administración ───────────────────────────────────────
        services.AddScoped<IPostulantesService, PostulantesService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMemberService, MemberService>();

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
