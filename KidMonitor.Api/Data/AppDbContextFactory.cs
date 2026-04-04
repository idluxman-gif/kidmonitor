using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KidMonitor.Api.Data;

// Design-time factory so "dotnet ef migrations add" works without a running DB.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=kidmonitor_design;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(opts);
    }
}
