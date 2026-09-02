using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// El reintento es lo que convierte el token de concurrencia de <c>Product</c> en
/// algo usable: protege de escrituras perdidas sin rechazar la mayoría de los
/// movimientos simultáneos sobre un mismo producto.
/// </summary>
public sealed class ConcurrencyRetryTests
{
    [Fact]
    public async Task Se_reintenta_hasta_que_la_operacion_tiene_exito()
    {
        using var context = TestDbContextFactory.Create();
        var attempts = 0;

        var result = await context.ExecuteWithConcurrencyRetryAsync((attempt, _) =>
        {
            attempts = attempt;

            // Falla dos veces, como si otra transacción se hubiera adelantado.
            if (attempt < 3)
                throw new DbUpdateConcurrencyException();

            return Task.FromResult("ok");
        });

        result.Should().Be("ok");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task El_numero_de_intento_llega_a_la_operacion_empezando_en_uno()
    {
        using var context = TestDbContextFactory.Create();
        var seen = new List<int>();

        await context.ExecuteWithConcurrencyRetryAsync((attempt, _) =>
        {
            seen.Add(attempt);

            if (attempt < 4)
                throw new DbUpdateConcurrencyException();

            return Task.FromResult(0);
        });

        seen.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task Si_el_conflicto_persiste_la_excepcion_se_propaga()
    {
        using var context = TestDbContextFactory.Create();
        var attempts = 0;

        // Agotados los intentos, la excepción sale y el middleware la traduce a 409:
        // es preferible un error explícito a reintentar indefinidamente.
        var act = async () => await context.ExecuteWithConcurrencyRetryAsync<int>((_, _) =>
        {
            attempts++;
            throw new DbUpdateConcurrencyException();
        });

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        // Se lee del propio contexto en vez de repetir el número: el tope subió al
        // medir contra SQL Server real y un literal aquí lo habría dejado obsoleto.
        attempts.Should().Be(ApplicationDbContext.MaxConcurrencyAttempts, "el tope de intentos");
    }

    [Fact]
    public async Task Otras_excepciones_no_se_reintentan()
    {
        using var context = TestDbContextFactory.Create();
        var attempts = 0;

        var act = async () => await context.ExecuteWithConcurrencyRetryAsync<int>((_, _) =>
        {
            attempts++;
            throw new InvalidOperationException("error de negocio");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        attempts.Should().Be(1, "sólo los conflictos de concurrencia justifican reintentar");
    }

    [Fact]
    public async Task Un_exito_al_primer_intento_no_reintenta()
    {
        using var context = TestDbContextFactory.Create();
        var attempts = 0;

        var result = await context.ExecuteWithConcurrencyRetryAsync((_, _) =>
        {
            attempts++;
            return Task.FromResult(42);
        });

        result.Should().Be(42);
        attempts.Should().Be(1);
    }
}
