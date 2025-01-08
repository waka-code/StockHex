namespace StockHex_API.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public  string Name { get; set; } = string.Empty;
        public   string Email { get; set; } = string.Empty;
        public  string Password { get; set; } = string.Empty;
        public  string Email_Confirmed { get; set; } = string.Empty;
        public  string Password_Confirmed { get; set; } = string.Empty;
        public  string Role { get; set; } = string.Empty;
        public  string Creation_date { get; set;} = string.Empty;
    }
}
