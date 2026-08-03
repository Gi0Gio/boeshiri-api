using Boeshiri.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api;

/// <summary>
/// Traduce <see cref="AppException"/> a una respuesta problem+json con su código
/// HTTP. Otras excepciones se dejan pasar al manejador por defecto (500 genérico),
/// sin exponer detalles internos (principio de no filtrado, RF-PUB-20).
/// </summary>
public sealed class AppExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException appException)
            return false;

        // Los 403 sí se registran: con un RBAC como este, los intentos de acceso
        // sin permiso son justo lo que conviene poder revisar después. El resto de
        // 4xx son ruido de uso normal.
        if (appException.StatusCode == 403)
        {
            logger.LogWarning("Acceso denegado a {Method} {Path} para {User}: {Motivo}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.User.FindFirst("sub")?.Value ?? "anónimo",
                appException.Message);
        }

        httpContext.Response.StatusCode = appException.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = appException,
            ProblemDetails = new ProblemDetails
            {
                Status = appException.StatusCode,
                Title = appException.Message
            }
        });
    }
}
