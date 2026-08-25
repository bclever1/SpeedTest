using Microsoft.EntityFrameworkCore;

namespace SpeedTest.Api;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SpeedTestResult> SpeedTestResults => Set<SpeedTestResult>();
}
