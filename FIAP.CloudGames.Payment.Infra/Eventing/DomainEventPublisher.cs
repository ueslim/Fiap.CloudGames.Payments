using FIAP.CloudGames.Core.DomainObjects;
using FIAP.CloudGames.Core.Mediator;

namespace FIAP.CloudGames.Payment.Infra.Eventing
{
    public static class DomainEventPublisher
    {
        public static async Task PublishAndClearAsync(this IMediatorHandler mediator, Entity aggregate)
        {
            if (aggregate?.Notifications == null) return;

            foreach (var @event in aggregate.Notifications)
                await mediator.PublishEvent(@event);

            aggregate.ClearEvents();
        }
    }
}