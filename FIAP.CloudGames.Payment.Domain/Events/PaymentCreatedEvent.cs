using FIAP.CloudGames.Core.Messages;

namespace FIAP.CloudGames.Payment.Domain.Events
{
    public class PaymentCreatedEvent : Event
    {
        public Guid PaymentId { get; }
        public Guid OrderId { get; }
        public decimal Value { get; }

        public PaymentCreatedEvent(Guid paymentId, Guid orderId, decimal value)
        {
            AggregateId = paymentId;
            PaymentId = paymentId;
            OrderId = orderId;
            Value = value;
        }
    }
}