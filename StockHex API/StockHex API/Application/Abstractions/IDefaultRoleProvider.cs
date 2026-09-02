namespace StockHex_API.Application.Abstractions;

/// <summary>
/// Resuelve el rol con el que se crean los usuarios del auto-registro público.
///
/// Con roles configurables ya no se puede codificar «Operator»: el rol es un dato,
/// puede renombrarse o borrarse. Se configura por nombre en
/// <c>Auth:RegistrationRoleName</c> y se resuelve al vuelo.
/// </summary>
public interface IDefaultRoleProvider
{
    /// <summary>Null cuando no hay rol configurado o el configurado no existe.</summary>
    Task<Guid?> GetRegistrationRoleIdAsync(CancellationToken cancellationToken = default);
}
