using System.Security.Cryptography;
using System.Text;

namespace Boeshiri.Infrastructure.Common;

/// <summary>
/// Tokens de un solo uso (verificar correo, restablecer contraseña, sesión). En la
/// base se guarda el hash: quien leyera una copia de la base no podría usarlos.
/// </summary>
public static class Tokens
{
    public static string Nuevo() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
