using FIAP.CloudGames.Core.DomainObjects;
using FIAP.CloudGames.Payment.Domain.Events;

namespace FIAP.CloudGames.Payment.Domain.Models
{
    public class Payment : Entity, IAggregateRoot
    {
        public Payment()
        {
            Transactions = new List<Transaction>();
        }

        public Guid OrderId { get; set; }
        public PaymentType PaymentType { get; set; }
        public decimal Value { get; set; }

        public CreditCard CreditCard { get; set; }

        // EF Relation
        public ICollection<Transaction> Transactions { get; set; }

        public void AddTransaction(Transaction transaction)
        {
            Transactions.Add(transaction);

            AddEvent(new TransactionAddedEvent(
                paymentId: this.Id,
                transactionId: transaction.Id,
                totalValue: transaction.TotalValue,
                status: (int)transaction.Status));
        }

        public void MarkTransactionCaptured(Transaction tx, decimal value)
        {
            tx.Status = TransactionStatus.Paid;
            AddEvent(new TransactionCapturedEvent(this.Id, tx.Id, value));
        }

        public void MarkTransactionCancelled(Transaction tx, decimal value)
        {
            tx.Status = TransactionStatus.Refunded;
            AddEvent(new TransactionCancelledEvent(this.Id, tx.Id, tx.TotalValue));
        }
    }
}