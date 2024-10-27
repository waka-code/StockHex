using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockHex_API.Infrastructure.Persistence
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer("Server=DESKTOP-HDP6M26\\WADDIMI;Database=StockHex;User Id=sa;Password=123456;TrustServerCertificate=True;");
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
