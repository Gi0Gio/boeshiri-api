namespace Boeshiri.Application.Abstractions;

/// <summary>Formato de la tarjeta para compartir.</summary>
public enum ShareFormat
{
    /// <summary>1080×1080. Para enviar en chats y grupos; también sirve de og:image.</summary>
    Square,

    /// <summary>1080×1920. Para historias de WhatsApp e Instagram.</summary>
    Story
}

/// <summary>Datos que se pintan en la tarjeta.</summary>
public record ShareCardContent(
    string Eyebrow,
    string Title,
    string? Subtitle,
    string? ImageUrl);

/// <summary>
/// Compone la tarjeta con la identidad de Boesh Irí para compartir en redes.
/// Se genera en el servidor porque los robots de WhatsApp e Instagram descargan
/// la imagen por su URL: no pueden ejecutar el front para producirla.
/// </summary>
public interface IShareCardRenderer
{
    /// <summary>Devuelve el PNG ya compuesto.</summary>
    Task<byte[]> RenderAsync(ShareCardContent content, ShareFormat format, CancellationToken ct = default);
}
