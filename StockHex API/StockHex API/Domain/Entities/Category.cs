using System.ComponentModel.DataAnnotations;

namespace StockHex_API.Domain.Entities
{
    public class Category
    {
        public Guid Id { get; set; }
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string Description { get; set; }
    }
}
