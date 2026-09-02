using FluentValidation;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Authorization;

namespace StockHex_API.Application.Validators;

/// <summary>Reglas compartidas por el alta y la edición de un rol.</summary>
internal static class RoleRules
{
    public static IRuleBuilderOptions<T, string> RoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("El nombre del rol es obligatorio.")
            .MaximumLength(80);

    public static IRuleBuilderOptions<T, IReadOnlyList<string>> PermissionKeys<T>(
        this IRuleBuilder<T, IReadOnlyList<string>> rule) =>
        rule.NotNull().WithMessage("La lista de permisos es obligatoria (puede ir vacía).")
            // Se valida contra el catálogo del código: una clave inventada no
            // protegería nada, así que se rechaza en lugar de guardarse.
            .Must(keys => keys is null || keys.All(Permissions.Exists))
            // La sobrecarga de dos parámetros da el valor de la propiedad; la de uno
            // entrega el objeto raíz, que aquí no sirve.
            .WithMessage((_, keys) =>
            {
                var unknown = Permissions.Unknown(keys ?? Array.Empty<string>());
                return unknown.Count == 0
                    ? "Hay permisos que no existen en el catálogo."
                    : $"Permisos desconocidos: {string.Join(", ", unknown)}.";
            });
}

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).RoleName();
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Permissions).PermissionKeys();
    }
}

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).RoleName();
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Permissions).PermissionKeys();
    }
}
