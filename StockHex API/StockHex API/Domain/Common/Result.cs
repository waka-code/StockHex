namespace StockHex_API.Domain.Common;

/// <summary>
/// Resultado de una operación sin valor de retorno. Las use cases devuelven
/// <see cref="Result"/> o <see cref="Result{T}"/> en lugar de lanzar excepciones
/// para el flujo esperado (no encontrado, duplicado, stock insuficiente).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("Un resultado exitoso no puede llevar error.");
        if (!isSuccess && error is null)
            throw new InvalidOperationException("Un resultado fallido requiere un error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Null cuando <see cref="IsSuccess"/> es true.</summary>
    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

/// <summary>Resultado de una operación que produce un valor.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, Error? error) : base(isSuccess, error)
        => _value = value;

    /// <summary>Sólo válido cuando <see cref="Result.IsSuccess"/> es true.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer Value de un resultado fallido.");

    public static Result<T> Success(T value) => new(true, value, null);

    public static new Result<T> Failure(Error error) => new(false, default, error);

    public static implicit operator Result<T>(T value) => Success(value);
}
