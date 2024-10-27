namespace StockHex_API.Domain.Entities
{
    public class InventoryMovement
    {
        public Guid Id { get; set; }
        public string Movement_type { get; set; }
        public int Product_id { get; set; }
        public int Quantity { get; set;}
        public string Movement_date { get; set; }
        public int User_id { get; set; }
        public int Client_id { get; set;}
        public string Comment { get; set;}

    }
}
