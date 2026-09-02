using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ClientUseCases;

public sealed class GetClients
{
    private readonly IClientRepository _clients;

    public GetClients(IClientRepository clients) => _clients = clients;

    public async Task<Result<PagedResponse<ClientResponse>>> RunAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await _clients.GetPagedAsync(request, cancellationToken);
        return PagedResponse<ClientResponse>.From(page, c => c.ToResponse());
    }
}
