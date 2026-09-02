using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ClientUseCases;

public sealed class GetClientById
{
    private readonly IClientRepository _clients;

    public GetClientById(IClientRepository clients) => _clients = clients;

    public async Task<Result<ClientResponse>> RunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var client = await _clients.GetByIdAsync(id, cancellationToken);

        return client is null
            ? Result<ClientResponse>.Failure(Error.NotFound("Cliente", id))
            : client.ToResponse();
    }
}
