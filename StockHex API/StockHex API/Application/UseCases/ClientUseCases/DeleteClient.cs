using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ClientUseCases;

public sealed class DeleteClient
{
    private readonly IClientRepository _clients;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteClient(IClientRepository clients, IUnitOfWork unitOfWork)
    {
        _clients = clients;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await _clients.GetByIdAsync(id, cancellationToken);
        if (client is null)
            return Result.Failure(Error.NotFound("Cliente", id));

        var movementCount = await _clients.CountMovementsAsync(id, cancellationToken);
        if (movementCount > 0)
            return Result.Failure(Error.Conflict(
                $"No se puede eliminar el cliente porque tiene {movementCount} movimiento(s) asociado(s)."));

        _clients.Remove(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
