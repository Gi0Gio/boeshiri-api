namespace Boeshiri.Domain;

/// <summary>
/// Catálogo cerrado de disciplinas: lo que filtra la Comunidad. Un miembro elige
/// una o varias; su forma de describirse («Muralismo, artista visual») sigue en
/// <c>User.Discipline</c> como texto libre. Las claves se guardan en la base:
/// renombrar una exige migrar los datos; las etiquetas se pueden cambiar sin más.
/// </summary>
public static class Disciplinas
{
    /// <summary>Clave → etiqueta, en el orden en que se muestran.</summary>
    public static readonly IReadOnlyList<(string Clave, string Etiqueta)> Catalogo =
    [
        ("dibujo", "Dibujo e ilustración"),
        ("pintura", "Pintura y muralismo"),
        ("foto", "Foto y audiovisual"),
        ("diseno", "Diseño"),
        ("escena", "Música y escena"),
        ("escritura", "Escritura"),
        ("artesania", "Artesanía"),
        ("tecnologia", "Tecnología"),
    ];

    private static readonly HashSet<string> Claves = Catalogo.Select(d => d.Clave).ToHashSet();

    public static bool Existe(string clave) => Claves.Contains(clave);

    /// <summary>Sin repetidas y en el orden del catálogo.</summary>
    public static List<string> Ordenar(IEnumerable<string> claves)
    {
        var elegidas = claves.ToHashSet();
        return Catalogo.Select(d => d.Clave).Where(elegidas.Contains).ToList();
    }
}
