using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FIAP.CloudGames.Payment.Infra.Data
{
    public class EventStoreSQLContextFactory : IDesignTimeDbContextFactory<EventStoreSqlContext>
    {
        public EventStoreSqlContext CreateDbContext(string[] args)
        {
            // volta uma pasta e entra na API
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "FIAP.CloudGames.Payment.API");

            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var cs = config.GetConnectionString("EventStoreConnection")
                     ?? "Server=(localdb)\\mssqllocaldb;Database=FIAPCloudGames_Payment;Trusted_Connection=True;TrustServerCertificate=True";

            var options = new DbContextOptionsBuilder<EventStoreSqlContext>()
                .UseSqlServer(cs, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_EventStore", "dbo"))
                .Options;

            return new EventStoreSqlContext(options);
        }
    }
}