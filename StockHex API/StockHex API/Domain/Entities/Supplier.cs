namespace StockHex_API.Domain.Entities
{
    public class Supplier
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Phone_Number { get; set; }
        public string Email { get; set; }
        public string[] Products { get; set; }
    }
}
