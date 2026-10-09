using Boeshiri.Application.Common;
using Boeshiri.Domain;

namespace Boeshiri.Infrastructure.Common;

/// <summary>Valida las disciplinas que llegan del perfil o de la postulación.</summary>
internal static class DisciplinasElegidas
{
    /// <summary>
    /// Sin repetidas y en el orden del catálogo. Una clave que no está en el catálogo
    /// es un 400: si se guardara, la persona quedaría fuera de todos los filtros.
    /// </summary>
    public static List<string> Validar(IEnumerable<string>? claves)
    {
        var lista = (claves ?? []).Select(c => c.Trim().ToLowerInvariant()).ToList();
        var desconocidas = lista.Where(c => !Disciplinas.Existe(c)).Distinct().ToList();
        if (desconocidas.Count > 0)
            throw AppException.BadRequest($"Disciplina desconocida: {string.Join(", ", desconocidas)}.");
        return Disciplinas.Ordenar(lista);
    }
}
