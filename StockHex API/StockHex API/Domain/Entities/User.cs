namespace StockHex_API.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public  required string Email { get; set; }
        public required string Password { get; set; }
        public required string Email_Confirmed { get; set; }
        public required string Password_Confirmed { get; set; }
        public required string Role { get; set; }
        public required string Creation_date { get; set;}
    }
}
