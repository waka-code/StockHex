using FluentAssertions;
using StockHex_API.Domain.Entities;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// Cada login y cada rotación añaden una fila a RefreshTokens. Sin purga la tabla
/// crece sin límite, así que la selección de qué se borra tiene que ser exacta.
/// </summary>
public sealed class RefreshTokenPurgeTests
{
    private static RefreshToken Token(
        Guid userId,
        DateTime expiresAt,
        DateTime? revokedAt = null) => new()
    {
        TokenHash = Guid.NewGuid().ToString(),
        UserId = userId,
        ExpiresAt = expiresAt,
        RevokedAt = revokedAt
    };

    [Fact]
    public async Task Se_borran_los_caducados_y_los_revocados_antes_del_corte()
    {
        using var context = TestDbContextFactory.Create();
        var user = TestData.User();
        context.Add(user);

        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-30);

        var caducadoAntiguo = Token(user.Id, expiresAt: now.AddDays(-40));
        var revocadoAntiguo = Token(user.Id, expiresAt: now.AddDays(10), revokedAt: now.AddDays(-35));
        var vigente = Token(user.Id, expiresAt: now.AddDays(10));
        var caducadoReciente = Token(user.Id, expiresAt: now.AddDays(-1));
        var revocadoReciente = Token(user.Id, expiresAt: now.AddDays(10), revokedAt: now.AddDays(-2));

        context.AddRange(caducadoAntiguo, revocadoAntiguo, vigente, caducadoReciente, revocadoReciente);
        await context.SaveChangesAsync();

        var repository = new RefreshTokenRepository(context);
        var removed = await repository.DeleteExpiredAsync(cutoff);
        await context.SaveChangesAsync();

        removed.Should().Be(2);

        var remaining = context.RefreshTokens.Select(t => t.Id).ToList();
        remaining.Should().BeEquivalentTo(new[] { vigente.Id, caducadoReciente.Id, revocadoReciente.Id },
            "los recientes se conservan para poder investigar un incidente");
    }

    [Fact]
    public async Task Sin_nada_que_borrar_no_reporta_cambios()
    {
        using var context = TestDbContextFactory.Create();
        var user = TestData.User();
        context.Add(user);
        context.Add(Token(user.Id, expiresAt: DateTime.UtcNow.AddDays(10)));
        await context.SaveChangesAsync();

        var removed = await new RefreshTokenRepository(context)
            .DeleteExpiredAsync(DateTime.UtcNow.AddDays(-30));

        removed.Should().Be(0);
        context.RefreshTokens.Should().HaveCount(1);
    }

    [Fact]
    public async Task Un_token_vigente_nunca_se_borra_aunque_sea_viejo()
    {
        using var context = TestDbContextFactory.Create();
        var user = TestData.User();
        context.Add(user);

        // Creado hace mucho pero con vencimiento futuro: sigue siendo una sesión activa.
        var vigente = Token(user.Id, expiresAt: DateTime.UtcNow.AddYears(1));
        vigente.CreatedAt = DateTime.UtcNow.AddYears(-1);
        context.Add(vigente);
        await context.SaveChangesAsync();

        var removed = await new RefreshTokenRepository(context)
            .DeleteExpiredAsync(DateTime.UtcNow.AddDays(-30));

        removed.Should().Be(0);
    }
}
