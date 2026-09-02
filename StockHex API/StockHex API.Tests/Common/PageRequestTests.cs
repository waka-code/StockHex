using FluentAssertions;
using StockHex_API.Domain.Common;

namespace StockHex_API.Tests.Common;

/// <summary>
/// La paginación acota sus propios valores para que ningún endpoint pueda
/// devolver la tabla completa por pasar pageSize=100000.
/// </summary>
public sealed class PageRequestTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void La_pagina_nunca_es_menor_a_uno(int input, int expected)
    {
        new PageRequest { Page = input }.Page.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, PageRequest.DefaultPageSize)]
    [InlineData(-1, PageRequest.DefaultPageSize)]
    [InlineData(50, 50)]
    [InlineData(1_000, PageRequest.MaxPageSize)]
    public void El_tamano_de_pagina_se_acota_al_maximo(int input, int expected)
    {
        new PageRequest { PageSize = input }.PageSize.Should().Be(expected);
    }

    [Fact]
    public void Skip_se_calcula_a_partir_de_la_pagina_y_el_tamano()
    {
        new PageRequest { Page = 3, PageSize = 20 }.Skip.Should().Be(40);
    }

    /// <summary>
    /// El defecto tiene que ser uno de los tamaños que la interfaz ofrece: si no,
    /// una pantalla recién abierta mostraría un número de filas que el selector
    /// no puede volver a elegir.
    /// </summary>
    [Fact]
    public void El_tamano_por_defecto_es_uno_de_los_ofrecidos()
    {
        PageRequest.AllowedPageSizes.Should().Contain(PageRequest.DefaultPageSize);
    }

    [Fact]
    public void Los_tamanos_ofrecidos_estan_dentro_del_techo_y_son_crecientes()
    {
        PageRequest.AllowedPageSizes.Should()
            .OnlyContain(size => size >= 1 && size <= PageRequest.MaxPageSize)
            .And.BeInAscendingOrder()
            .And.OnlyHaveUniqueItems()
            .And.NotBeEmpty();
    }
}

public sealed class PagedResultTests
{
    [Fact]
    public void Los_metadatos_de_navegacion_reflejan_la_posicion_en_el_conjunto()
    {
        var page = new PagedResult<int>([1, 2, 3], totalCount: 10, page: 2, pageSize: 3);

        page.TotalPages.Should().Be(4);
        page.HasPrevious.Should().BeTrue();
        page.HasNext.Should().BeTrue();
    }

    [Fact]
    public void Una_pagina_vacia_no_ofrece_navegacion()
    {
        var page = new PagedResult<int>([], totalCount: 0, page: 1, pageSize: 20);

        page.TotalPages.Should().Be(0);
        page.HasPrevious.Should().BeFalse();
        page.HasNext.Should().BeFalse();
    }

    [Fact]
    public void La_ultima_pagina_no_ofrece_siguiente()
    {
        var page = new PagedResult<int>([9, 10], totalCount: 10, page: 5, pageSize: 2);

        page.TotalPages.Should().Be(5);
        page.HasPrevious.Should().BeTrue();
        page.HasNext.Should().BeFalse();
    }
}

public sealed class ResultTests
{
    [Fact]
    public void Leer_el_valor_de_un_resultado_fallido_lanza()
    {
        var result = Result<int>.Failure(Error.NotFound("Producto", 1));

        result.IsFailure.Should().BeTrue();
        var read = () => result.Value;
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Un_valor_se_convierte_implicitamente_en_resultado_exitoso()
    {
        Result<string> result = "ok";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
        result.Error.Should().BeNull();
    }
}
