using Boeshiri.Application.Abstractions;
using System.Linq.Expressions;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Marketplace;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Auth;
using Boeshiri.Infrastructure.Common;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Boeshiri.Infrastructure.Marketplace;

/// <summary>Marketplace (§9): catálogo, alta, gestión propia, moderación y compartir.</summary>
public class MarketplaceService(
    BoeshiriDbContext db,
    IAuditLogger audit,
    IFileStorage storage,
    IOptions<AppOptions> appOptions) : IMarketplaceService
{
    private const int MaxImages = 5;
    private readonly AppOptions _app = appOptions.Value;

    public async Task EnrollAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.Unauthorized("Usuario no encontrado.");
        user.MarketplaceActive = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> ListPublicAsync(string? name, string? category, Guid? sellerId, CancellationToken ct = default)
    {
        var query = db.Products.Where(p => p.Status == ProductStatus.Published
            && p.Seller.Status != MemberStatus.Suspended && p.Seller.Status != MemberStatus.Expelled);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var lowered = name.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(lowered));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        // Filtro por vendedor: alimenta el "ver todo lo de esta persona" desde la
        // ficha de un anuncio o desde su perfil.
        if (sellerId is not null)
            query = query.Where(p => p.SellerId == sellerId);

        return await query.OrderByDescending(p => p.CreatedAt).Select(ToSummary).ToListAsync(ct);
    }

    public async Task<ProductDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Products
            .Include(x => x.Seller).ThenInclude(s => s.SocialLinks)
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (p is null || p.Status is ProductStatus.Hidden or ProductStatus.Deleted || !Visibilidad.AutorVisible(p.Seller.Status))
            throw AppException.NotFound("El producto no está disponible.");

        return new ProductDetailDto(
            p.Id, p.Kind, p.Name, p.Category, p.Price, p.PriceMax, p.Description, p.DeliveryLocation, p.Status,
            p.SellerId, p.Seller.FullName, Contact(p.Seller),
            p.Images.OrderBy(i => i.Order).Select(i => i.Url).ToList());
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Products
            .Where(p => p.SellerId == userId && p.Status != ProductStatus.Deleted)
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToSummary)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> ListForModerationAsync(CancellationToken ct = default)
    {
        return await db.Products
            .Where(p => p.Status != ProductStatus.Deleted)
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToSummary)
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateAsync(Guid userId, CreateProductRequest request, CancellationToken ct = default)
    {
        var enrolled = await db.Users.Where(u => u.Id == userId).Select(u => u.MarketplaceActive).FirstOrDefaultAsync(ct);
        if (!enrolled)
            throw AppException.Forbidden("Debes darte de alta en el marketplace antes de publicar (RF-MKT-03).");

        if ((request.Images?.Count ?? 0) > MaxImages)
            throw AppException.BadRequest($"Máximo {MaxImages} imágenes por producto.");

        ValidatePriceRange(request.Kind, request.Price, request.PriceMax);
        ArchivosGuard.ExigirPropias(storage, request.Images, null, ArchivosGuard.CarpetasImagen, "Imágenes");

        var product = new Product
        {
            SellerId = userId,
            Kind = request.Kind,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Price = request.Price,
            PriceMax = request.PriceMax,
            Description = request.Description,
            DeliveryLocation = request.DeliveryLocation
        };

        var order = 0;
        foreach (var url in request.Images ?? [])
            product.Images.Add(new ProductImage { Url = url, Order = order++ });

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return product.Id;
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateProductRequest request, CancellationToken ct = default)
    {
        var p = await db.Products
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId, ct)
            ?? throw AppException.NotFound("El producto no existe o no es tuyo.");

        // El tipo no cambia al editar, así que el rango se valida contra el que tiene.
        ValidatePriceRange(p.Kind, request.Price, request.PriceMax);

        if (request.Images is not null && request.Images.Count > MaxImages)
            throw AppException.BadRequest($"Máximo {MaxImages} imágenes por producto.");
        ArchivosGuard.ExigirPropias(storage, request.Images, p.Images.Select(i => i.Url), ArchivosGuard.CarpetasImagen, "Imágenes");

        p.Name = request.Name.Trim();
        p.Category = request.Category.Trim();
        p.Price = request.Price;
        p.PriceMax = request.PriceMax;
        p.Description = request.Description;
        p.DeliveryLocation = request.DeliveryLocation;
        p.EditedAt = DateTime.UtcNow;

        var eliminadas = new List<string>();
        if (request.Images is not null)
        {
            // Se comparan contra las actuales en vez de vaciar y recrear: vaciar la
            // colección deja huérfanos que EF resuelve por su cuenta, y destruiría
            // filas que no cambiaron.
            var actuales = p.Images.ToList();
            eliminadas = actuales.Select(i => i.Url).Except(request.Images).ToList();

            foreach (var img in actuales.Where(i => eliminadas.Contains(i.Url)))
                db.ProductImages.Remove(img);

            var order = 0;
            foreach (var url in request.Images)
            {
                var existente = actuales.FirstOrDefault(i => i.Url == url);
                if (existente is not null) existente.Order = order++;
                else db.ProductImages.Add(new ProductImage { ProductId = p.Id, Url = url, Order = order++ });
            }
        }

        await db.SaveChangesAsync(ct);

        // Después de guardar: si el borrado remoto falla queda un huérfano, no una
        // referencia rota.
        foreach (var url in eliminadas)
            await ArchivosGuard.BorrarSiSinUsoAsync(db, storage, url, ct);
    }

    public async Task ChangeStatusAsync(Guid id, ProductStatusAction action, Guid userId, bool canModerate, CancellationToken ct = default)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El producto no está disponible.");

        var isOwner = p.SellerId == userId;

        // Un servicio no se agota: sin disponibilidad lo que corresponde es ocultarlo.
        if (action == ProductStatusAction.Sold && p.Kind == ListingKind.Service)
            throw AppException.BadRequest("Un servicio no se marca como vendido. Ocúltalo mientras no tengas disponibilidad.");

        // Marcar vendido: solo el dueño. Mostrar: el dueño, o un moderador que
        // revierte su propia ocultación. Ocultar / eliminar: dueño o moderador (RF-MKT-08).
        var allowed = action switch
        {
            ProductStatusAction.Sold => isOwner,
            _ => isOwner || canModerate
        };
        if (!allowed)
            throw AppException.Forbidden("No tienes permiso para cambiar el estado de este producto.");

        if (p.Status == ProductStatus.Deleted)
            throw AppException.Conflict("El anuncio fue eliminado y no se puede recuperar.");

        // Vendido también lo vuelve visible en su ficha: con la moderación encima,
        // ni mostrar ni marcar vendido le corresponden al dueño.
        if (action is ProductStatusAction.Show or ProductStatusAction.Sold && p.ModeratedAt is not null && !canModerate)
            throw AppException.Forbidden("La moderación ocultó este anuncio. Escribe a la Junta si crees que fue un error.");

        var comoModerador = !isOwner && canModerate;
        switch (action)
        {
            case ProductStatusAction.Hide:
                p.Status = ProductStatus.Hidden;
                if (comoModerador) p.ModeratedAt = DateTime.UtcNow;
                break;
            case ProductStatusAction.Show:
                p.Status = ProductStatus.Published;
                p.ModeratedAt = null;
                break;
            case ProductStatusAction.Sold:
                p.Status = ProductStatus.Sold;
                break;
            case ProductStatusAction.Delete:
                p.Status = ProductStatus.Deleted;
                if (comoModerador) p.ModeratedAt = DateTime.UtcNow;
                break;
        }

        if (canModerate && !isOwner)
            audit.Log(userId, "producto.moderado", "Product", p.Id.ToString(), action.ToString());

        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────
    /// <summary>
    /// El rango de precios existe solo para servicios: su costo depende del alcance
    /// del trabajo. Un bien físico tiene un precio y punto.
    /// </summary>
    private static void ValidatePriceRange(ListingKind kind, decimal price, decimal? priceMax)
    {
        if (priceMax is null) return;

        if (kind != ListingKind.Service)
            throw AppException.BadRequest("El rango de precios solo aplica a los servicios.");

        if (priceMax < price)
            throw AppException.BadRequest("El precio máximo no puede ser menor que el mínimo.");
    }

    private static SellerContactDto Contact(User seller) => new(
        seller.ShowEmail ? seller.Email : null,
        seller.ShowPhone ? seller.Phone : null,
        seller.SocialLinks
            .Where(l => l.Visible && (l.Type != SocialNetworkType.Whatsapp || seller.ShowWhatsapp))
            .Select(l => new ContactLinkDto(l.Type, l.Value))
            .ToList());

    private static readonly Expression<Func<Product, ProductSummaryDto>> ToSummary = p => new ProductSummaryDto(
        p.Id, p.Kind, p.Name, p.Category, p.Price, p.PriceMax, p.SellerId, p.Seller.FullName, p.Status,
        p.Images.OrderBy(i => i.Order).Select(i => i.Url).FirstOrDefault());
}
