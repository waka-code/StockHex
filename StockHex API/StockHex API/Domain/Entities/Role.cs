using System.ComponentModel.DataAnnotations;

namespace StockHex_API.Domain.Entities
{
    public class Role
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
        public string Permise { get; set; }
    }
}
