namespace Boeshiri.Domain.Enums;

/// <summary>Biblioteca a la que pertenece un documento (RF-DOC-03).</summary>
public enum DocumentLibrary
{
    /// <summary>Material interno aportado libremente por los miembros.</summary>
    Community,

    /// <summary>Plantillas y cartas membretadas suministradas por la Junta.</summary>
    Administration
}

/// <summary>Nivel de acceso de un documento (RF-DOC-02/05).</summary>
public enum DocumentAccessLevel
{
    /// <summary>Accesible a cualquier miembro.</summary>
    Members,

    /// <summary>Restringido a Administración (Junta / Super).</summary>
    Administration
}
