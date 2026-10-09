using Microsoft.Extensions.Caching.Memory;

namespace Boeshiri.Infrastructure.Auth;

/// <summary>
/// Freno a la fuerza bruta por CUENTA: tras <see cref="MaxFallos"/> contraseñas
/// incorrectas seguidas, esa cuenta no admite más intentos durante
/// <see cref="Ventana"/>. Complementa al límite por IP del pipeline, que por sí solo
/// no basta: quien prueba contraseñas puede repartir los intentos entre IPs.
///
/// El coste asumido: alguien puede bloquear a propósito el inicio de sesión de otra
/// persona durante 15 minutos. Es preferible a dejar la contraseña a merced de
/// intentos ilimitados, y quien quede bloqueado puede recuperar la contraseña.
/// </summary>
public sealed class LoginThrottle(IMemoryCache cache)
{
    public const int MaxFallos = 5;
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);

    private static string Clave(string email) => "login-fallos:" + email;

    public bool Bloqueada(string email) =>
        cache.TryGetValue(Clave(email), out int fallos) && fallos >= MaxFallos;

    public void RegistrarFallo(string email)
    {
        var fallos = cache.TryGetValue(Clave(email), out int previos) ? previos + 1 : 1;
        cache.Set(Clave(email), fallos, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = Ventana,
        });
    }

    public void Limpiar(string email) => cache.Remove(Clave(email));
}
