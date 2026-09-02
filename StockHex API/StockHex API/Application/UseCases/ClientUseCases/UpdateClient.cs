using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ClientUseCases;

public sealed class UpdateClient
{
    private readonly IClientRepository _clients;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateClient(IClientRepository clients, IUnitOfWork unitOfWork)
    {
        _clients = clients;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ClientResponse>> RunAsync(
        Guid id,
        UpdateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await _clients.GetByIdAsync(id, cancellationToken);
        if (client is null)
            return Result<ClientResponse>.Failure(Error.NotFound("Cliente", id));

        var email = request.Email?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(email) &&
            await _clients.ExistsByEmailAsync(email, id, cancellationToken))
            return Result<ClientResponse>.Failure(
                Error.Conflict($"Ya existe otro cliente con el email '{email}'."));

        client.Name = request.Name.Trim();
        client.Address = request.Address?.Trim();
        client.PhoneNumber = request.PhoneNumber?.Trim();
        client.Email = email;
        client.UpdatedAt = DateTime.UtcNow;

        _clients.Update(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return client.ToResponse();
    }
}
