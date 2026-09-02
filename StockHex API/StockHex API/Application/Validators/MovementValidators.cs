using FluentValidation;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Enums;

namespace StockHex_API.Application.Validators;

public sealed class CreateMovementRequestValidator : AbstractValidator<CreateMovementRequest>
{
    public CreateMovementRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("El producto es obligatorio.");

        RuleFor(x => x.MovementType)
            .IsInEnum().WithMessage("El tipo de movimiento debe ser In (1), Out (2) o Adjustment (3).");

        // Entradas y salidas mueven unidades, así que deben ser positivas.
        // Un ajuste fija el stock final, y dejarlo en 0 es legítimo.
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0.")
            .When(x => x.MovementType != MovementType.Adjustment);

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("El stock ajustado no puede ser negativo.")
            .When(x => x.MovementType == MovementType.Adjustment);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.")
            .When(x => x.UnitPrice.HasValue);

        RuleFor(x => x.Comment).MaximumLength(500);

        // Un movimiento tiene como máximo una contraparte. No se restringe por tipo
        // porque ambas combinaciones son legítimas: una devolución a proveedor es una
        // salida con proveedor, y una devolución de cliente es una entrada con cliente.
        // Atarlo al tipo hacía que la reversión de una compra produjera un estado
        // (salida con proveedor) que este mismo endpoint rechazaba.
        RuleFor(x => x.ClientId)
            .Null()
            .WithMessage("Un movimiento no puede tener cliente y proveedor a la vez.")
            .When(x => x.SupplierId.HasValue);
    }
}

public sealed class ReverseMovementRequestValidator : AbstractValidator<ReverseMovementRequest>
{
    public ReverseMovementRequestValidator()
    {
        RuleFor(x => x.Comment).MaximumLength(400);
    }
}
