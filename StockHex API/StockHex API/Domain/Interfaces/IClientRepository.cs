using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces
{
    public interface IClientRepository
    {
            Task<Client> GetClientById(Guid id);
            Task<IEnumerable<Client>> GetAllClient();
            Task<Client> PostClient(Client client);
            Task<Client> UpdateClientById(Client client);
            Task DeleteClientById(Guid id);
        }
}
