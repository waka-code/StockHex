using FluentValidation;
using StockHex_API.Application.DTOs;

namespace StockHex_API.Application.Validators;

/// <summary>Reglas de contraseña compartidas por registro, alta de usuario y cambio de contraseña.</summary>
internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(150);

        RuleFor(x => x.Password).Password();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Las contraseñas no coinciden.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("El rol es obligatorio.");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(150);

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("El rol es obligatorio.");
    }
}

/// <summary>
/// Restablecimiento hecho por otra persona: no hay contraseña actual que validar,
/// pero la nueva cumple las mismas reglas de fuerza.
/// </summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).Password();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Las contraseñas no coinciden.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("La contraseña actual es obligatoria.");

        RuleFor(x => x.NewPassword)
            .Password()
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("La contraseña nueva debe ser distinta de la actual.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Las contraseñas no coinciden.");
    }
}
