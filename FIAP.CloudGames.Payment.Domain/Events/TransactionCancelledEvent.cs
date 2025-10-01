using FIAP.CloudGames.Core.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
