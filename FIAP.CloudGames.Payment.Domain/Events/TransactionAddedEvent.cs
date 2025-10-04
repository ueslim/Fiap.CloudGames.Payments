using FIAP.CloudGames.Core.Messages;

namespace FIAP.CloudGames.Payment.Domain.Events
{
    public class TransactionAddedEvent : Event
    {
        public Guid PaymentId { get; }
        public Guid TransactionId { get; }
        public decimal TotalValue { get; }
        public int Status { get; }

        public TransactionAddedEvent(Guid paymentId, Guid transactionId, decimal totalValue, int status)
        {
            AggregateId = paymentId;
            PaymentId = paymentId;
            TransactionId = transactionId;
            TotalValue = totalValue;
            Status = status;
        }
    }
}