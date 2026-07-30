namespace Boeshiri.Infrastructure.Storage;

/// <summary>
/// Configuración de Cloudflare R2 (sección "R2"). Las llaves van en secrets/env,
/// nunca en el repo. Si <see cref="AccountId"/> está vacío, el almacenamiento
/// queda deshabilitado y las subidas devuelven un error claro.
/// </summary>
public class R2Options
{
    public const string SectionName = "R2";

    /// <summary>ID de cuenta de Cloudflare (forma el endpoint S3 de R2).</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Access Key ID del token de R2 (Object Read &amp; Write).</summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>Secret Access Key del token de R2.</summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>Nombre del bucket.</summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>URL pública base del bucket (r2.dev o dominio propio), sin barra final.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(Bucket) &&
        !string.IsNullOrWhiteSpace(PublicBaseUrl);
}
