using FIAP.CloudGames.Core.Data;

namespace FIAP.CloudGames.Payment.Domain.Models
{
    public interface IPaymentRepository : IRepository<Payment>
    {
        Task<IEnumerable<Payment>> GetAll();

        Task<Payment> GetPaymentByOrderId(Guid orderId);

        Task<IEnumerable<Transaction>> GetTransactionsByOrderId(Guid orderId);

        void AddPayment(Payment payment);

        void AddTransaction(Transaction transaction);
    }
}