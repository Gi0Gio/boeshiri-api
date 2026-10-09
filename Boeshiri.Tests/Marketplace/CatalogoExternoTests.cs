using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Marketplace;

/// <summary>
/// El modelo del catálogo compartido (ADR-0007) depende de que Product.UpdatedAt
/// avance con cualquier cambio: es lo que una web externa pide para traer solo lo nuevo.
/// </summary>
public class CatalogoExternoTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task UpdatedAt_AdvancesOnEditStatusAndImageChanges()
    {
        Guid id;
        DateTime alta;
        await using (var c = _db.CreateContext())
        {
            var u = new User { Email = "v@ex.com", PasswordHash = "x", FullName = "V", EmailVerified = true, Status = MemberStatus.Active };
            var p = new Product { Seller = u, Name = "Taza", Category = "Cerámica", Price = 10 };
            c.Products.Add(p);
            await c.SaveChangesAsync();
            id = p.Id;
            alta = p.UpdatedAt;
            Assert.True(p.AllowExternalListing);
        }

        await Task.Delay(15);
        DateTime trasEditar;
        await using (var c = _db.CreateContext())
        {
            var p = await c.Products.SingleAsync(x => x.Id == id);
            p.Status = ProductStatus.Sold;
            await c.SaveChangesAsync();
            trasEditar = p.UpdatedAt;
        }
        Assert.True(trasEditar > alta);

        await Task.Delay(15);
        await using (var c = _db.CreateContext())
        {
            // Solo la foto, sin cargar el producto: el DbContext tiene que encontrarlo igual.
            c.ProductImages.Add(new ProductImage { ProductId = id, Url = "https://bucket/marketplace/a.webp", Order = 0 });
            await c.SaveChangesAsync();
        }
        await using var check = _db.CreateContext();
        Assert.True((await check.Products.SingleAsync(x => x.Id == id)).UpdatedAt > trasEditar);
    }

    [Fact]
    public async Task ExternalLink_SameExternalIdTwiceOnOneConnection_IsRejected()
    {
        await using var c = _db.CreateContext();
        var u = new User { Email = "v@ex.com", PasswordHash = "x", FullName = "V", EmailVerified = true, Status = MemberStatus.Active };
        var con = new CatalogConnection { Name = "Web de catálogo", Provider = "json-feed", Direction = CatalogSyncDirection.Both };
        var a = new Product { Seller = u, Name = "A", Category = "X", Price = 1 };
        var b = new Product { Seller = u, Name = "B", Category = "X", Price = 1 };
        c.AddRange(con, a, b);
        c.ProductExternalLinks.Add(new ProductExternalLink { Product = a, Connection = con, Role = ExternalLinkRole.Imported, ExternalId = "sku-1" });
        c.ProductExternalLinks.Add(new ProductExternalLink { Product = b, Connection = con, Role = ExternalLinkRole.Imported, ExternalId = "sku-1" });
        await Assert.ThrowsAsync<DbUpdateException>(() => c.SaveChangesAsync());
    }

    public void Dispose() => _db.Dispose();
}
