namespace Boeshiri.Domain.Entities;

/// <summary>
/// Imagen de una publicación (hasta 3 en artículo/noticia, ≥1 en foto). La URL
/// apunta al object storage; el límite de 5 MB y formatos se valida al subir (RF-IMG-01).
/// </summary>
public class PublicationImage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;

    public required string Url { get; set; }
    public int Order { get; set; }
}
