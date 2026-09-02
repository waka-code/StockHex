using Microsoft.EntityFrameworkCore;
using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;

namespace StockHex_API.Infrastructure.Persistence;

/// <summary>
/// Llena todos los módulos con datos de demostración, <b>para mirar la aplicación
/// en local</b>: una base vacía no enseña nada —los listados no paginan, los
/// reportes salen en cero y la matriz de permisos no tiene a quién aplicarse.
///
/// <para>
/// <b>No se ejecuta salvo que se pida.</b> Hace falta <c>Seed:DemoData=true</c>
/// (o <c>Seed__DemoData=true</c> por entorno) y por omisión está apagado. No se ata
/// al entorno <c>Development</c> a propósito: el compose local arranca como
/// <c>Production</c>, así que gatillarlo por entorno no serviría aquí y daría una
/// falsa sensación de seguridad. La protección real es que hay que encenderlo a mano.
/// </para>
///
/// <para>
/// <b>Respeta el invariante del dominio.</b> Ningún producto nace con existencias:
/// se crean en cero y el stock que acaban teniendo es el acumulado de sus propios
/// movimientos, con <c>StockBefore</c> y <c>StockAfter</c> encadenados. Sembrar un
/// stock suelto habría producido un historial que no cuadra con el saldo, que es
/// exactamente lo que este sistema existe para impedir.
/// </para>
/// </summary>
public sealed class DemoDataSeeder
{
    /// <summary>Contraseña de todos los usuarios de demostración. Cumple el validador.</summary>
    public const string DemoPassword = "Demo1234";

    /// <summary>
    /// Marca de agua: si esta categoría existe, ya se sembró. Evita duplicar en cada
    /// arranque sin necesidad de una tabla de control.
    /// </summary>
    private const string MarkerCategory = "Abarrotes";

    /// <summary>Semilla fija: la demo es idéntica en cada máquina y en cada reinicio.</summary>
    private const int Seed = 20260902;

    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DemoDataSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue("Seed:DemoData", false))
            return;

        if (await _context.Categories.AnyAsync(c => c.Name == MarkerCategory, cancellationToken))
        {
            _logger.LogInformation("Los datos de demostración ya estaban sembrados.");
            return;
        }

        var random = new Random(Seed);
        var hoy = DateTime.UtcNow.Date;

        var categorias = Categorias();
        var proveedores = Proveedores();
        var clientes = Clientes();
        _context.AddRange(categorias);
        _context.AddRange(proveedores);
        _context.AddRange(clientes);

        var roles = await RolesDemoAsync(cancellationToken);
        _context.AddRange(roles.Nuevos);

        var usuarios = Usuarios(roles.PorNombre);
        _context.AddRange(usuarios);

        var productos = Productos(categorias, proveedores, random);
        _context.AddRange(productos);

        var movimientos = Movimientos(productos, proveedores, clientes, usuarios, hoy, random);
        _context.AddRange(movimientos);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "DATOS DE DEMOSTRACIÓN SEMBRADOS — {Cat} categorías, {Prov} proveedores, " +
            "{Cli} clientes, {Prod} productos, {Usr} usuarios y {Mov} movimientos. " +
            "Seed:DemoData está encendido: apágalo en cualquier entorno que no sea local. " +
            "Para empezar de cero: docker compose down -v.",
            categorias.Count, proveedores.Count, clientes.Count,
            productos.Count, usuarios.Count, movimientos.Count);

        _logger.LogWarning(
            "Usuarios de demostración: {Emails} — todos con la contraseña {Password}.",
            string.Join(", ", usuarios.Select(u => u.Email)), DemoPassword);
    }

    // ─────────────────────────────────────────────────── catálogo

    private static List<Category> Categorias() =>
    [
        new() { Name = MarkerCategory, Description = "Despensa seca: arroz, fideos, legumbres" },
        new() { Name = "Bebidas", Description = "Aguas, jugos y bebidas gaseosas" },
        new() { Name = "Lácteos", Description = "Leche, quesos y yogures" },
        new() { Name = "Limpieza", Description = "Detergentes y artículos de aseo" },
        new() { Name = "Snacks", Description = "Galletas, papas fritas y confites" },
        new() { Name = "Congelados", Description = "Cadena de frío" },
        new() { Name = "Panadería", Description = "Pan y masas" },
        new() { Name = "Higiene personal", Description = "Cuidado personal" },
    ];

    private static List<Supplier> Proveedores() =>
    [
        new() { Name = "Comercial Andes", Description = "Abarrotes al por mayor", PhoneNumber = "+56 2 2345 6789", Email = "ventas@comercialandes.cl" },
        new() { Name = "Distribuidora Pacífico", Description = "Bebidas y jugos", PhoneNumber = "+56 2 2987 6543", Email = "pedidos@dpacifico.cl" },
        new() { Name = "Lácteos del Sur", Description = "Cadena de frío desde Osorno", PhoneNumber = "+56 64 233 4455", Email = "contacto@lacteosdelsur.cl" },
        new() { Name = "Química Austral", Description = "Línea de limpieza", PhoneNumber = "+56 2 2555 1200", Email = "ventas@quimicaaustral.cl" },
        new() { Name = "Importadora Kanto", Description = "Snacks y confites importados", PhoneNumber = "+56 2 2777 3311", Email = "kanto@importadorakanto.cl" },
        new() { Name = "Frío Express", Description = "Congelados y helados", PhoneNumber = "+56 2 2444 8899", Email = "logistica@frioexpress.cl" },
        new() { Name = "Molinos Maipo", Description = "Harinas y panadería", PhoneNumber = "+56 2 2666 1020", Email = "maipo@molinosmaipo.cl" },
        new() { Name = "Higiene Total", Description = "Cuidado personal", PhoneNumber = "+56 2 2333 7744", Email = "ventas@higienetotal.cl" },
    ];

    private static List<Client> Clientes() =>
    [
        new() { Name = "Almacén Don Pedro", Address = "Av. Matta 1240, Santiago", PhoneNumber = "+56 9 8123 4567", Email = "donpedro@correo.cl" },
        new() { Name = "Minimarket La Esquina", Address = "Los Leones 55, Providencia", PhoneNumber = "+56 9 8222 3344", Email = "laesquina@correo.cl" },
        new() { Name = "Botillería El Roble", Address = "San Diego 890, Santiago", PhoneNumber = "+56 9 8333 1122", Email = "elroble@correo.cl" },
        new() { Name = "Panadería Santa Rosa", Address = "Santa Rosa 2310, San Miguel", PhoneNumber = "+56 9 8444 9900", Email = "santarosa@correo.cl" },
        new() { Name = "Casino Industrial Vitacura", Address = "Vitacura 4500", PhoneNumber = "+56 2 2900 1100", Email = "casino@vitacura.cl" },
        new() { Name = "Almacén Las Rejas", Address = "Las Rejas 780, Estación Central", PhoneNumber = "+56 9 8555 6677", Email = "lasrejas@correo.cl" },
        new() { Name = "Distribuidora Ñuñoa", Address = "Irarrázaval 3120, Ñuñoa", PhoneNumber = "+56 9 8666 2233", Email = "dnunoa@correo.cl" },
        new() { Name = "Kiosco Plaza Brasil", Address = "Plaza Brasil s/n, Santiago", PhoneNumber = "+56 9 8777 4455", Email = "plazabrasil@correo.cl" },
        new() { Name = "Restaurante El Fogón", Address = "Bellavista 210, Recoleta", PhoneNumber = "+56 9 8888 5566", Email = "elfogon@correo.cl" },
        new() { Name = "Supermercado Maipú", Address = "Pajaritos 2900, Maipú", PhoneNumber = "+56 2 2811 4400", Email = "compras@supermaipu.cl" },
        new() { Name = "Cafetería Lastarria", Address = "Lastarria 90, Santiago", PhoneNumber = "+56 9 8999 1010", Email = "lastarria@correo.cl" },
        new() { Name = "Almacén Puente Alto", Address = "Concha y Toro 1500, Puente Alto", PhoneNumber = "+56 9 8010 2020", Email = "puentealto@correo.cl" },
        new() { Name = "Hotel Cerro Alegre", Address = "Urriola 500, Valparaíso", PhoneNumber = "+56 32 259 8800", Email = "compras@cerroalegre.cl" },
        new() { Name = "Feria Libre Recoleta", Address = "Recoleta 1800", PhoneNumber = "+56 9 8030 4040", Email = "ferialibre@correo.cl" },
    ];

    // ─────────────────────────────────────────────── roles y usuarios

    /// <summary>
    /// Los tres roles base los crea la migración. Aquí se añaden dos personalizados
    /// para que la pantalla de Roles muestre lo que de verdad la distingue: roles
    /// que alguien creó, con permisos elegidos uno a uno.
    /// </summary>
    private async Task<(List<Role> Nuevos, Dictionary<string, Role> PorNombre)> RolesDemoAsync(
        CancellationToken cancellationToken)
    {
        var existentes = await _context.Roles.Include(r => r.Permissions).ToListAsync(cancellationToken);
        var porNombre = existentes.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        var nuevos = new List<Role>();

        Role Crear(string nombre, string descripcion, IEnumerable<string> permisos)
        {
            var rol = new Role { Name = nombre, Description = descripcion, IsSystem = false };
            foreach (var clave in Permissions.Normalize(permisos))
                rol.Permissions.Add(new RolePermission { RoleId = rol.Id, Permission = clave });
            nuevos.Add(rol);
            porNombre[nombre] = rol;
            return rol;
        }

        if (!porNombre.ContainsKey("Cajero"))
            Crear("Cajero", "Registra salidas del mostrador y consulta el catálogo",
            [
                Permissions.Dashboard.View, Permissions.Products.View,
                Permissions.Movements.View, Permissions.Movements.Create,
                Permissions.Clients.View,
            ]);

        if (!porNombre.ContainsKey("Auditor"))
            Crear("Auditor", "Sólo lectura: revisa historial y reportes sin poder tocarlos",
            [
                Permissions.Dashboard.View, Permissions.Products.View,
                Permissions.Movements.View, Permissions.Reports.View,
                Permissions.Categories.View, Permissions.Suppliers.View,
                Permissions.Clients.View, Permissions.Users.View, Permissions.Roles.View,
            ]);

        return (nuevos, porNombre);
    }

    private List<User> Usuarios(Dictionary<string, Role> roles)
    {
        Role Rol(string preferido, string alternativo) =>
            roles.TryGetValue(preferido, out var r) ? r : roles[alternativo];

        var jefe = Rol("Jefe de bodega", "Administrador");
        var bodeguero = Rol("Bodeguero", "Administrador");
        var cajero = Rol("Cajero", "Bodeguero");
        var auditor = Rol("Auditor", "Bodeguero");

        User Nuevo(string nombre, string email, Role rol, bool activo = true) => new()
        {
            Name = nombre,
            Email = email,
            PasswordHash = _passwordHasher.Hash(DemoPassword),
            RoleId = rol.Id,
            Role = rol,
            IsActive = activo,
            EmailConfirmed = true,
            LastLoginAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 6)),
        };

        return
        [
            Nuevo("Carolina Reyes", "carolina@stockhex.local", jefe),
            Nuevo("Matías Fuentes", "matias@stockhex.local", bodeguero),
            Nuevo("Javiera Soto", "javiera@stockhex.local", cajero),
            Nuevo("Rodrigo Peña", "rodrigo@stockhex.local", auditor),
            // Uno desactivado: así el filtro por estado tiene algo que mostrar y se
            // puede comprobar que su token deja de valer.
            Nuevo("Ignacio Bravo", "ignacio@stockhex.local", bodeguero, activo: false),
        ];
    }

    // ─────────────────────────────────────────────────── productos

    private sealed record Plantilla(string Sku, string Nombre, string Categoria, int Proveedor, decimal Precio, int Minimo);

    private static List<Product> Productos(
        List<Category> categorias, List<Supplier> proveedores, Random random)
    {
        var porCategoria = categorias.ToDictionary(c => c.Name);

        var plantillas = new List<Plantilla>
        {
            new("ARROZ-1K",  "Arroz grado 1, 1 kg",            "Abarrotes", 0, 1290, 40),
            new("FIDEO-400", "Fideos spaghetti 400 g",         "Abarrotes", 0,  890, 50),
            new("AZUC-1K",   "Azúcar granulada 1 kg",          "Abarrotes", 0, 1150, 40),
            new("LENT-500",  "Lentejas 500 g",                 "Abarrotes", 0, 1690, 25),
            new("ACEI-900",  "Aceite vegetal 900 ml",          "Abarrotes", 0, 2390, 30),
            new("SAL-1K",    "Sal de mar 1 kg",                "Abarrotes", 0,  690, 20),
            new("HARI-1K",   "Harina sin polvos 1 kg",         "Abarrotes", 6, 1090, 35),
            new("AGUA-500",  "Agua mineral 500 ml",            "Bebidas",   1,  590, 80),
            new("AGUA-15L",  "Agua mineral 1,5 L",             "Bebidas",   1,  990, 60),
            new("BEBI-15L",  "Bebida cola 1,5 L",              "Bebidas",   1, 1690, 60),
            new("JUGO-1L",   "Jugo de naranja 1 L",            "Bebidas",   1, 1390, 40),
            new("ENER-473",  "Bebida energética 473 ml",       "Bebidas",   1, 1490, 30),
            new("CERV-350",  "Cerveza lager 350 ml",           "Bebidas",   1, 1190, 48),
            new("LECH-1L",   "Leche entera 1 L",               "Lácteos",   2, 1190, 60),
            new("LECH-DES",  "Leche descremada 1 L",           "Lácteos",   2, 1190, 40),
            new("YOGU-125",  "Yogur natural 125 g",            "Lácteos",   2,  450, 90),
            new("QUES-250",  "Queso gauda 250 g",              "Lácteos",   2, 2990, 20),
            new("MANT-250",  "Mantequilla 250 g",              "Lácteos",   2, 2490, 18),
            new("CREM-200",  "Crema de leche 200 ml",          "Lácteos",   2,  990, 24),
            new("DETE-3L",   "Detergente líquido 3 L",         "Limpieza",  3, 5990, 15),
            new("CLOR-900",  "Cloro gel 900 ml",               "Limpieza",  3, 1290, 25),
            new("LAVA-750",  "Lavaloza 750 ml",                "Limpieza",  3, 1890, 30),
            new("PAPE-12",   "Papel higiénico 12 rollos",      "Limpieza",  3, 6490, 20),
            new("ESPO-3",    "Esponjas multiuso, pack 3",      "Limpieza",  3,  990, 30),
            new("TOAL-2",    "Toalla de papel, pack 2",        "Limpieza",  3, 2290, 22),
            new("GALL-160",  "Galletas de vainilla 160 g",     "Snacks",    4,  790, 45),
            new("PAPA-250",  "Papas fritas 250 g",             "Snacks",    4, 2190, 30),
            new("CHOC-100",  "Chocolate de leche 100 g",       "Snacks",    4, 1490, 35),
            new("MANI-200",  "Maní salado 200 g",              "Snacks",    4, 1190, 25),
            new("CARA-150",  "Caramelos surtidos 150 g",       "Snacks",    4,  890, 30),
            new("HELA-1L",   "Helado de vainilla 1 L",         "Congelados", 5, 3490, 12),
            new("PAPA-CONG", "Papas prefritas congeladas 1 kg", "Congelados", 5, 2790, 18),
            new("VERD-CONG", "Verduras mixtas congeladas 1 kg", "Congelados", 5, 2290, 15),
            new("EMPA-12",   "Empanadas congeladas, pack 12",  "Congelados", 5, 5990, 10),
            new("PANM-500",  "Pan de molde 500 g",             "Panadería", 6, 1790, 35),
            new("MARR-6",    "Marraquetas, pack 6",            "Panadería", 6, 1290, 40),
            new("HALL-12",   "Hallullas, pack 12",             "Panadería", 6, 1990, 30),
            new("MASA-EMP",  "Masa para empanadas, pack 12",   "Panadería", 6, 2490, 15),
            new("SHAM-400",  "Shampoo anticaspa 400 ml",       "Higiene personal", 7, 4290, 18),
            new("JABO-3",    "Jabón de tocador, pack 3",       "Higiene personal", 7, 1890, 25),
            new("PAST-90",   "Pasta dental 90 g",              "Higiene personal", 7, 1690, 30),
            new("DESO-150",  "Desodorante spray 150 ml",       "Higiene personal", 7, 3290, 20),
            new("CEPI-2",    "Cepillo dental, pack 2",         "Higiene personal", 7, 2190, 20),
            new("AFEI-4",    "Máquinas de afeitar, pack 4",    "Higiene personal", 7, 2890, 15),
            // Descatalogados: dan contenido al filtro «inactivos» y al guardia que
            // impide borrar un producto con historial.
            new("BEBI-DIET", "Bebida cola dietética 2 L",      "Bebidas",   1, 1890, 20),
            new("QUES-MANT", "Queso mantecoso 250 g",          "Lácteos",   2, 2790, 15),
        };

        var productos = new List<Product>();

        foreach (var (plantilla, indice) in plantillas.Select((p, i) => (p, i)))
        {
            productos.Add(new Product
            {
                Sku = plantilla.Sku,
                Name = plantilla.Nombre,
                Description = $"{plantilla.Nombre} · {plantilla.Categoria}",
                Price = plantilla.Precio,
                MinimumStock = plantilla.Minimo,
                // Nace en cero, siempre: el stock lo construyen los movimientos.
                StockQuantity = 0,
                CategoryId = porCategoria[plantilla.Categoria].Id,
                Category = porCategoria[plantilla.Categoria],
                SupplierId = proveedores[plantilla.Proveedor].Id,
                Supplier = proveedores[plantilla.Proveedor],
                IsActive = indice < plantillas.Count - 2,
                CreatedAt = DateTime.UtcNow.AddDays(-90 + random.Next(0, 10)),
            });
        }

        return productos;
    }

    // ─────────────────────────────────────────────────── movimientos

    /// <summary>
    /// Construye el libro mayor de los últimos 90 días. El stock de cada producto se
    /// va acumulando aquí y se escribe al final: es la misma aritmética que hace
    /// <c>CreateMovement</c>, así que el historial y el saldo cuadran fila a fila.
    /// </summary>
    private static List<InventoryMovement> Movimientos(
        List<Product> productos,
        List<Supplier> proveedores,
        List<Client> clientes,
        List<User> usuarios,
        DateTime hoy,
        Random random)
    {
        var movimientos = new List<InventoryMovement>();
        var autores = usuarios.Where(u => u.IsActive).ToList();

        // Uno de cada tres se deja por debajo del mínimo con una última venta grande:
        // sin eso el reporte de stock bajo y el aviso del panel salen siempre vacíos.
        var dejarBajoMinimo = productos.Where((_, i) => i % 3 == 1).ToHashSet();

        // Último día usado por producto, para poder colgar la reversión al final de
        // su línea de tiempo y no en medio de ella.
        var ultimoDia = new Dictionary<Guid, int>();

        foreach (var producto in productos)
        {
            var stock = 0;
            var dia = 88;

            void Asentar(
                MovementType tipo, int cantidad, int nuevoStock,
                Guid? clienteId, Guid? proveedorId, string? comentario)
            {
                movimientos.Add(new InventoryMovement
                {
                    ProductId = producto.Id,
                    Product = producto,
                    MovementType = tipo,
                    Quantity = cantidad,
                    UnitPrice = tipo == MovementType.In
                        ? Math.Round(producto.Price * 0.72m, 0)
                        : tipo == MovementType.Out ? producto.Price : null,
                    StockBefore = stock,
                    StockAfter = nuevoStock,
                    MovementDate = hoy.AddDays(-dia)
                        .AddHours(random.Next(8, 19))
                        .AddMinutes(random.Next(0, 60)),
                    UserId = autores[random.Next(autores.Count)].Id,
                    ClientId = clienteId,
                    SupplierId = proveedorId,
                    Comment = comentario,
                });
                stock = nuevoStock;
            }

            // 1 · carga inicial
            var inicial = Math.Max(producto.MinimumStock * 3, 30);
            Asentar(MovementType.In, inicial, inicial, null, producto.SupplierId,
                $"Carga inicial · OC-{1000 + random.Next(1, 900)}");

            // 2 · el vaivén del trimestre: ventas y reposiciones alternadas.
            //     `dia` sólo decrece, así que la fecha sólo avanza y la cadena
            //     StockBefore → StockAfter queda en orden por construcción.
            // El paso se acota a lo que queda para hoy, así los ciclos llegan hasta
            // ayer en vez de pararse a mitad de camino: un panel cuyo último
            // movimiento es de hace dos semanas parece un sistema abandonado.
            var ciclos = random.Next(6, 12);
            for (var c = 0; c < ciclos && dia > 2; c++)
            {
                dia -= Math.Min(dia - 1, random.Next(3, 9));

                var venta = Math.Min(stock, random.Next(3, Math.Max(4, producto.MinimumStock)));
                if (venta > 0)
                {
                    var cliente = clientes[random.Next(clientes.Count)];
                    Asentar(MovementType.Out, venta, stock - venta, cliente.Id, null,
                        $"Venta · BOL-{7000 + random.Next(1, 2000)}");
                }

                if (dia > 2)
                {
                    dia -= Math.Min(dia - 1, random.Next(1, 4));
                    var compra = random.Next(producto.MinimumStock, producto.MinimumStock * 2 + 10);
                    Asentar(MovementType.In, compra, stock + compra, null, producto.SupplierId,
                        $"Reposición · OC-{1000 + random.Next(1, 900)}");
                }
            }

            // 3 · un conteo físico en uno de cada siete: da contenido al tipo Ajuste
            if (random.Next(0, 7) == 0 && dia > 1)
            {
                dia -= 1;
                var contado = Math.Max(0, stock - random.Next(1, 4));
                Asentar(MovementType.Adjustment, contado, contado, null, null,
                    "Conteo físico de bodega");
            }

            // 4 · el pedido grande que vacía la estantería. Es lo que hace que el
            //     reporte de stock bajo tenga algo que mostrar.
            if (dejarBajoMinimo.Contains(producto) && stock > producto.MinimumStock && dia > 1)
            {
                dia -= 1;
                var objetivo = random.Next(0, Math.Max(1, producto.MinimumStock));
                var venta = stock - objetivo;
                var cliente = clientes[random.Next(clientes.Count)];
                Asentar(MovementType.Out, venta, objetivo, cliente.Id, null,
                    $"Pedido mayorista · BOL-{7000 + random.Next(1, 2000)}");
            }

            producto.StockQuantity = stock;
            producto.UpdatedAt = movimientos[^1].MovementDate;
            ultimoDia[producto.Id] = dia;
        }

        // 5 · dos correcciones reales: un movimiento equivocado no se borra, se
        //     revierte. La reversión se cuelga DESPUÉS del último movimiento de su
        //     producto —se descubre el error más tarde y se corrige entonces—; si se
        //     colara en medio, su StockBefore no encajaría con la fila anterior.
        //     Se eligen productos holgados para no sacar del reporte a los que
        //     acaban de quedar bajo mínimo.
        var candidatos = movimientos
            .Where(m => m.MovementType == MovementType.Out
                        && m.Quantity > 0
                        && !dejarBajoMinimo.Contains(m.Product!))
            .GroupBy(m => m.ProductId)
            .Select(g => g.First())
            .OrderBy(_ => random.Next())
            .Take(2)
            .ToList();

        foreach (var original in candidatos)
        {
            var producto = original.Product!;
            var devuelto = original.Quantity;
            var dia = Math.Max(0, ultimoDia[producto.Id] - 1);

            movimientos.Add(new InventoryMovement
            {
                ProductId = producto.Id,
                Product = producto,
                MovementType = MovementType.In,
                Quantity = devuelto,
                UnitPrice = original.UnitPrice,
                StockBefore = producto.StockQuantity,
                StockAfter = producto.StockQuantity + devuelto,
                MovementDate = hoy.AddDays(-dia).AddHours(random.Next(9, 18)),
                UserId = original.UserId,
                ClientId = original.ClientId,
                SupplierId = original.SupplierId,
                ReversalOfMovementId = original.Id,
                ReversalOfMovement = original,
                Comment = $"Reversión de {original.MovementType} de {original.Quantity} " +
                          $"del {original.MovementDate:yyyy-MM-dd HH:mm} UTC. Boleta anulada.",
            });

            producto.StockQuantity += devuelto;
        }

        return movimientos;
    }
}
