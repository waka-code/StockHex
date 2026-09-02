namespace StockHex_API.Domain.Common;

/// <summary>Clasificación del error, usada para elegir el status HTTP de la respuesta.</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unexpected
}

/// <summary>
/// Error de negocio devuelto por una use case. <see cref="Errors"/> se llena sólo
/// cuando el error es de validación y hay detalle por campo.
/// </summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static Error NotFound(string entity, object key) =>
        new("not_found", $"{entity} con identificador '{key}' no fue encontrado.", ErrorType.NotFound);

    public static Error NotFound(string message) =>
        new("not_found", message, ErrorType.NotFound);

    public static Error Conflict(string message) =>
        new("conflict", message, ErrorType.Conflict);

    public static Error Validation(string message) =>
        new("validation_error", message, ErrorType.Validation);

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new("validation_error", "Uno o más campos son inválidos.", ErrorType.Validation, errors);

    public static Error Unauthorized(string message) =>
        new("unauthorized", message, ErrorType.Unauthorized);

    public static Error Forbidden(string message) =>
        new("forbidden", message, ErrorType.Forbidden);
}
