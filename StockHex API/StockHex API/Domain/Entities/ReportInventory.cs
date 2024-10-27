namespace StockHex_API.Domain.Entities
{
    public class ReportInventory
    {
        public Guid Id { get; set; }
        public string Start_date { get; set; }
        public string End_date { get; set; }
        public string Report_type { get; set; }
        public string User_id { get; set;}
        public string Details { get; set;}

    }
}
