using FluentValidation;
using StockHex_API.Application.DTOs;

namespace StockHex_API.Application.Validators;

public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Address).MaximumLength(250);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Address).MaximumLength(250);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
