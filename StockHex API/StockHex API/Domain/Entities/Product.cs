using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StockHex_API.Domain.Entities
{
    public class Product
    {
        public Guid Id { get; set; }

        [MaxLength(50)]
        public  string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Description { get; set; }

        [MaxLength(100)]
        public string Sku { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal Price { get; set; }

        public int Stock_quantity { get; set; }
        public string Category_id { get; set; }
        public string Supplier_id { get; set; }

        [MaxLength(50)]
        public string Creation_date { get; set; }

        [MaxLength(50)]
        public string Update_date { get; set; }

    }
}
