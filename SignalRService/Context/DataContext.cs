using Microsoft.EntityFrameworkCore;
using SignalRService.Models;

namespace SignalRService.Context
{
    public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure your entity mappings here
        }
        public DbSet<WhitelistedIP> WhitelistedIPs { get; set; }
    }
}
