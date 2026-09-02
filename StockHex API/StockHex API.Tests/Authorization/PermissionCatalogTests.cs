using System.Text.RegularExpressions;
using FluentAssertions;
using StockHex_API.Domain.Authorization;

namespace StockHex_API.Tests.Authorization;

/// <summary>
/// El catálogo es la única fuente de permisos (regla 7). Estos tests fijan sus
/// invariantes y, sobre todo, comprueban que la migración que siembra los roles
/// iniciales no se desincronice del código: es el único sitio donde las claves
/// se escriben a mano en SQL.
/// </summary>
public sealed class PermissionCatalogTests
{
    [Fact]
    public void Todas_las_claves_son_unicas()
    {
        Permissions.Catalog.Select(d => d.Key)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Todas_las_claves_tienen_forma_modulo_punto_accion()
    {
        foreach (var descriptor in Permissions.Catalog)
        {
            descriptor.Key.Should().Be($"{descriptor.Module}.{descriptor.Action}",
                "la clave se construye con el módulo y la acción");
            descriptor.Key.Should().MatchRegex("^[a-z]+\\.[a-z_]+$");
        }
    }

    [Fact]
    public void Cada_modulo_concede_el_permiso_de_ver()
    {
        // Sin 'view' el resto de acciones del módulo son inalcanzables: no se
        // puede abrir la pantalla para usarlas.
        foreach (var group in Permissions.Catalog.GroupBy(d => d.Module))
        {
            group.Select(d => d.Action).Should().Contain("view",
                $"el módulo '{group.Key}' necesita un permiso de lectura");
        }
    }

    [Fact]
    public void Las_acciones_especiales_son_las_que_no_estan_en_la_rejilla()
    {
        var standard = new[] { "view", "create", "edit", "delete" };

        foreach (var descriptor in Permissions.Catalog)
        {
            descriptor.IsSpecial.Should().Be(!standard.Contains(descriptor.Action),
                $"'{descriptor.Key}' está marcado como especial de forma incoherente");
        }
    }

    [Fact]
    public void Los_permisos_criticos_estan_en_el_catalogo()
    {
        foreach (var permission in Permissions.Critical)
            Permissions.Exists(permission).Should().BeTrue($"'{permission}' debe existir");
    }

    [Fact]
    public void Normalize_descarta_lo_desconocido_y_ordena_como_el_catalogo()
    {
        var result = Permissions.Normalize(
        [
            Permissions.Reports.View,
            "inventado.total",
            Permissions.Dashboard.View,
            Permissions.Products.View,
            Permissions.Dashboard.View,   // duplicado
        ]);

        result.Should().Equal(
            Permissions.Dashboard.View,
            Permissions.Products.View,
            Permissions.Reports.View);
    }

    [Fact]
    public void Unknown_reporta_sólo_las_claves_que_no_existen()
    {
        Permissions.Unknown([Permissions.Users.View, "users.superpoder", ""])
            .Should().Equal("users.superpoder");
    }

    // ─────────────────────────────────── coherencia con la migración

    private static string MigrationSource()
    {
        // Se busca hacia arriba desde el binario hasta encontrar el proyecto de la API.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "StockHex API", "Migrations")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("hay que poder localizar la carpeta de migraciones");

        var file = Directory
            .GetFiles(Path.Combine(directory!.FullName, "StockHex API", "Migrations"),
                      "*_AddRolesAndPermissions.cs")
            .Single();

        return File.ReadAllText(file);
    }

    [Fact]
    public void La_migracion_no_siembra_permisos_que_no_existan_en_el_catalogo()
    {
        var source = MigrationSource();

        // Las claves aparecen en el SQL como ('...', '<rol>', '<permiso>')
        var seeded = Regex.Matches(source, @"'([a-z]+\.[a-z_]+)'")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        seeded.Should().NotBeEmpty("la migración debe sembrar permisos");

        var unknown = seeded.Where(k => !Permissions.Exists(k)).ToList();
        unknown.Should().BeEmpty(
            "la migración escribe las claves a mano en SQL; si el catálogo cambia de nombre " +
            "una clave, la siembra queda apuntando a un permiso que ya no existe");
    }

    [Fact]
    public void El_rol_de_sistema_de_la_migracion_recibe_el_catalogo_completo()
    {
        var source = MigrationSource();

        // El bloque del rol de sistema es el del id 1111…
        var adminKeys = Regex.Matches(source,
                @"\(NEWID\(\), '11111111-1111-1111-1111-111111111111', '([a-z]+\.[a-z_]+)'\)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        adminKeys.Should().BeEquivalentTo(Permissions.All,
            "el rol de sistema es el último recurso para administrar: se queda sin sentido " +
            "si el catálogo crece y la migración no le concede el permiso nuevo");
    }

    [Fact]
    public void El_rol_de_menor_privilegio_no_puede_administrar()
    {
        var source = MigrationSource();

        var operatorKeys = Regex.Matches(source,
                @"\(NEWID\(\), '33333333-3333-3333-3333-333333333333', '([a-z]+\.[a-z_]+)'\)")
            .Select(m => m.Groups[1].Value)
            .ToList();

        operatorKeys.Should().NotBeEmpty();
        operatorKeys.Should().NotContain(Permissions.Critical,
            "el rol del auto-registro no puede venir con permisos de administración");
        operatorKeys.Should().NotContain(k => k.StartsWith("users.") || k.StartsWith("roles."));
    }
}
