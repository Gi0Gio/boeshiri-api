namespace Boeshiri.Application.Common;

/// <summary>
/// Excepción de aplicación con un código HTTP asociado. La capa de presentación
/// la traduce a una respuesta problem+json (ver AppExceptionHandler).
/// </summary>
public class AppException(int statusCode, string message) : Exception(message)
{
    /// <summary>Código de estado HTTP con el que responder.</summary>
    public int StatusCode { get; } = statusCode;

    public static AppException BadRequest(string message) => new(StatusCodes.BadRequest, message);
    public static AppException Unauthorized(string message) => new(StatusCodes.Unauthorized, message);
    public static AppException Forbidden(string message) => new(StatusCodes.Forbidden, message);
    public static AppException NotFound(string message) => new(StatusCodes.NotFound, message);
    public static AppException Conflict(string message) => new(StatusCodes.Conflict, message);

    private static class StatusCodes
    {
        public const int BadRequest = 400;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int Conflict = 409;
    }
}
