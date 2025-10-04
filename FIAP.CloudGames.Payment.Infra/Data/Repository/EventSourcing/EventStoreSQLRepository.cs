using FIAP.CloudGames.Core.Events;
using Microsoft.EntityFrameworkCore;

namespace FIAP.CloudGames.Payment.Infra.Data.Repository.EventSourcing
{
    public class EventStoreSqlRepository : IEventStoreRepository
    {
        private readonly EventStoreSqlContext _context;

        public EventStoreSqlRepository(EventStoreSqlContext context)
        {
            _context = context;
        }

        public async Task<IList<StoredEvent>> All(Guid aggregateId)
        {
            return await (from e in _context.StoredEvents where e.AggregateId == aggregateId select e).ToListAsync();
        }

        public void Store(StoredEvent theEvent)
        {
            try
            {
                _context.StoredEvents.Add(theEvent);
                _context.SaveChanges();
            }
            catch (Exception e)
            {
                var eita = e.Message;
                throw;
            }
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}