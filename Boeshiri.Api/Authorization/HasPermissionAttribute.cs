using Microsoft.AspNetCore.Authorization;

namespace Boeshiri.Api.Authorization;

/// <summary>
/// Exige un permiso global concreto en un endpoint. Ej.:
/// <c>[HasPermission("postulantes.decidir")]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) => Policy = $"perm:{permission}";
}
