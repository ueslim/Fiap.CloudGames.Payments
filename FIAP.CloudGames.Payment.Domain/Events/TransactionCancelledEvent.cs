using FIAP.CloudGames.Core.Messages;

namespace FIAP.CloudGames.Payment.Domain.Events
{
    public class TransactionCancelledEvent : Event
    {
        public Guid PaymentId { get; }
        public Guid TransactionId { get; }
        public decimal Value { get; }

        public TransactionCancelledEvent(Guid paymentId, Guid transactionId, decimal value)
        {
            AggregateId = paymentId;
            PaymentId = paymentId;
            TransactionId = transactionId;
            Value = value;
        }
    }
}