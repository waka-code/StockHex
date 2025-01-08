using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Domain.Services
{
    public class ClientService
    {
        private readonly IClientRepository _repository;

        public ClientService(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<Client> GetClientById(Guid id)
        {
            var client = await _repository.GetClientById(id);
            if (client == null)
                throw new KeyNotFoundException($"Client Not Found: {id}");
            return client;
        }

        public async Task<IEnumerable<Client>> GetAllClient()
        {
            return await _repository.GetAllClient();
        }

        public async Task<Client> PostClient(Client client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client), "client cannot be null.");
            return await _repository.PostClient(client);
        }

        public async Task<Client> UpdateClientById(Client client)
        {
            var clientExist = await _repository.GetClientById(client.Id);
            if (clientExist == null)
            {
                throw new KeyNotFoundException($"client with ID not found: {client.Id}");
            }


            clientExist.Name = client.Name;
            clientExist.Phone_Number = client.Phone_Number;
            clientExist.Address = client.Address;
            clientExist.Email = client.Email;
            return await _repository.UpdateClientById(clientExist);
        }

        public async Task DeleteProductById(Guid id)
        {
            var deleteClient = await _repository.GetClientById(id);
            if (deleteClient == null)
            {
                throw new KeyNotFoundException($"client with ID: {id} was not found.");
            }
            await _repository.DeleteClientById(deleteClient.Id);
        }
    }
}
