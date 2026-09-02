using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ClientUseCases;

public sealed class CreateClient
{
    private readonly IClientRepository _clients;
    private readonly IUnitOfWork _unitOfWork;

    public CreateClient(IClientRepository clients, IUnitOfWork unitOfWork)
    {
        _clients = clients;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ClientResponse>> RunAsync(
        CreateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(email) &&
            await _clients.ExistsByEmailAsync(email, null, cancellationToken))
            return Result<ClientResponse>.Failure(
                Error.Conflict($"Ya existe un cliente con el email '{email}'."));

        var client = new Client
        {
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Email = email
        };

        await _clients.AddAsync(client, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return client.ToResponse();
    }
}
