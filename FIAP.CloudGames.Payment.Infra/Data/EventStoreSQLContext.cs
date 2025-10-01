using FIAP.CloudGames.Core.Events;
using FIAP.CloudGames.Payment.Infra.Data.Mappings.EventSourcing;
using Microsoft.EntityFrameworkCore;

namespace FIAP.CloudGames.Payment.Infra.Data
{
    public class EventStoreSqlContext : DbContext
    {
        public EventStoreSqlContext(DbContextOptions<EventStoreSqlContext> options) : base(options)
        {
        }

        public DbSet<StoredEvent> StoredEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("payment");

            modelBuilder.ApplyConfiguration(new StoredEventMap());

            base.OnModelCreating(modelBuilder);
        }
    }
}