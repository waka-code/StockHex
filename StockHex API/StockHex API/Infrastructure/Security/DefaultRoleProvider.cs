using Microsoft.EntityFrameworkCore;
using StockHex_API.Application.Abstractions;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Security;

public sealed class DefaultRoleProvider : IDefaultRoleProvider
{
    /// <summary>Rol de menor privilegio que crea la migración inicial.</summary>
    public const string FallbackRoleName = "Bodeguero";

    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DefaultRoleProvider> _logger;

    public DefaultRoleProvider(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<DefaultRoleProvider> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Guid?> GetRegistrationRoleIdAsync(CancellationToken cancellationToken = default)
    {
        var name = _configuration["Auth:RegistrationRoleName"];
        if (string.IsNullOrWhiteSpace(name))
            name = FallbackRoleName;

        var role = await _context.Roles
            .AsNoTracking()
            .Where(r => r.Name == name)
            .Select(r => new { r.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            // Se avisa en lugar de caer en el primer rol que aparezca: elegir uno
            // al azar podría dar permisos de administración a cualquiera que se registre.
            _logger.LogWarning(
                "No existe el rol '{Role}' configurado para el auto-registro. " +
                "El registro público quedará deshabilitado hasta configurar Auth:RegistrationRoleName.",
                name);
            return null;
        }

        return role.Id;
    }
}
