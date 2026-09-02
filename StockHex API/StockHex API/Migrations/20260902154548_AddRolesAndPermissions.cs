using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockHex_API.Migrations;

/// <summary>
/// RBAC configurable: los roles pasan de un enum del código a filas en la base.
///
/// El orden importa y es el motivo de que esta migración esté escrita a mano en
/// lugar de aceptar la que generó EF: la que generó EF borraba la columna Role
/// antes de crear nada y dejaba a todos los usuarios con RoleId vacío, lo que
/// habría violado la FK y perdido la asignación de cada uno.
///
/// Aquí se crean las tablas, se insertan los tres roles equivalentes al enum, se
/// traspasa la asignación usuario por usuario y sólo entonces se borra la columna
/// vieja y se impone la FK. Nadie pierde acceso al desplegar.
/// </summary>
public partial class AddRolesAndPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─────────────────────────────────────────── 1 · tablas nuevas
        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                IsSystem = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Roles", x => x.Id));

        // Sin FK a una tabla de permisos: el catálogo vive en el código y no tiene
        // tabla. La clave se valida contra Permissions.All antes de escribir.
        migrationBuilder.CreateTable(
            name: "RolePermissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Permission = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_RolePermissions_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Name", table: "Roles", column: "Name", unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_RoleId_Permission",
            table: "RolePermissions",
            columns: new[] { "RoleId", "Permission" },
            unique: true);

        // ──────────────────────────── 2 · los tres roles y sus permisos
        migrationBuilder.Sql(@"-- Los tres roles equivalentes al enum anterior, con ids fijos para que
-- reaplicar la migración produzca los mismos roles en cualquier entorno.
INSERT INTO [Roles] ([Id], [Name], [Description], [IsSystem], [CreatedAt])
VALUES
    ('11111111-1111-1111-1111-111111111111', N'Administrador', N'Acceso total al sistema', 1, SYSUTCDATETIME()),
    ('22222222-2222-2222-2222-222222222222', N'Jefe de bodega', N'Catálogo, contrapartes, movimientos y reportes', 0, SYSUTCDATETIME()),
    ('33333333-3333-3333-3333-333333333333', N'Bodeguero', N'Registra movimientos y consulta el catálogo', 0, SYSUTCDATETIME());

INSERT INTO [RolePermissions] ([Id], [RoleId], [Permission])
VALUES
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'dashboard.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'products.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'products.create'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'products.edit'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'products.delete'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'movements.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'movements.create'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'movements.reverse'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'categories.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'categories.create'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'categories.edit'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'categories.delete'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'suppliers.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'suppliers.create'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'suppliers.edit'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'suppliers.delete'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'clients.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'clients.create'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'clients.edit'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'clients.delete'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'reports.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'reports.export'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'users.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'users.create'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'users.edit'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'users.delete'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'users.change_password'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'roles.view'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'roles.create'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'roles.edit'),
                (NEWID(), '11111111-1111-1111-1111-111111111111', 'roles.delete'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'dashboard.view'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'products.view'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'products.create'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'products.edit'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'products.delete'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'movements.view'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'movements.create'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'movements.reverse'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'categories.view'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'categories.create'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'categories.edit'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'categories.delete'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'suppliers.view'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'suppliers.create'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'suppliers.edit'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'suppliers.delete'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'clients.view'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'clients.create'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'clients.edit'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'clients.delete'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'reports.view'),
                (NEWID(), '22222222-2222-2222-2222-222222222222', 'reports.export'),
                (NEWID(), '33333333-3333-3333-3333-333333333333', 'dashboard.view'),
                (NEWID(), '33333333-3333-3333-3333-333333333333', 'products.view'),
                (NEWID(), '33333333-3333-3333-3333-333333333333', 'movements.view'),
                (NEWID(), '33333333-3333-3333-3333-333333333333', 'movements.create'),
                (NEWID(), '33333333-3333-3333-3333-333333333333', 'reports.view');");

        // ─────────── 3 · columna nueva, nullable para poder rellenarla
        migrationBuilder.AddColumn<Guid>(
            name: "RoleId",
            table: "Users",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql(@"-- Cada usuario recibe el rol equivalente al valor que tenía en la columna Role.
-- Nadie pierde acceso: es el mismo conjunto de capacidades con otro nombre.
UPDATE [Users] SET [RoleId] = CASE [Role]
    WHEN 'Admin'    THEN '11111111-1111-1111-1111-111111111111'
    WHEN 'Manager'  THEN '22222222-2222-2222-2222-222222222222'
    WHEN 'Operator' THEN '33333333-3333-3333-3333-333333333333'
    -- Un valor inesperado cae al rol de menor privilegio, nunca al de sistema.
    ELSE '33333333-3333-3333-3333-333333333333'
END;");

        // ────── 4 · ahora que todos tienen rol, se impone la restricción
        migrationBuilder.AlterColumn<Guid>(
            name: "RoleId",
            table: "Users",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.DropColumn(name: "Role", table: "Users");

        migrationBuilder.CreateIndex(
            name: "IX_Users_RoleId", table: "Users", column: "RoleId");

        // Restrict: borrar un rol no debe arrastrar a sus usuarios.
        migrationBuilder.AddForeignKey(
            name: "FK_Users_Roles_RoleId",
            table: "Users",
            column: "RoleId",
            principalTable: "Roles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Se recupera la columna del enum antes de soltar la relación, para no
        // dejar a los usuarios sin rol en el camino de vuelta.
        migrationBuilder.AddColumn<string>(
            name: "Role",
            table: "Users",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Operator");

        migrationBuilder.Sql(@"-- Se recupera el texto del enum a partir del rol asignado.
UPDATE [Users] SET [Role] = CASE [RoleId]
    WHEN '11111111-1111-1111-1111-111111111111'    THEN 'Admin'
    WHEN '22222222-2222-2222-2222-222222222222'  THEN 'Manager'
    WHEN '33333333-3333-3333-3333-333333333333' THEN 'Operator'
    ELSE 'Operator'
END;");

        migrationBuilder.DropForeignKey(name: "FK_Users_Roles_RoleId", table: "Users");
        migrationBuilder.DropIndex(name: "IX_Users_RoleId", table: "Users");
        migrationBuilder.DropColumn(name: "RoleId", table: "Users");

        migrationBuilder.DropTable(name: "RolePermissions");
        migrationBuilder.DropTable(name: "Roles");
    }
}
