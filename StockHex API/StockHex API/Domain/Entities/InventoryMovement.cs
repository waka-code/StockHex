namespace StockHex_API.Domain.Entities
{
    public class InventoryMovement
    {
        public Guid Id { get; set; }
        public string Movement_type { get; set; }
        public string Product_id { get; set; }
        public int Quantity { get; set;}
        public string Movement_date { get; set; }
        public string User_id { get; set; }
        public string Client_id { get; set;}
        public string Comment{ get; set;}

    }
}
