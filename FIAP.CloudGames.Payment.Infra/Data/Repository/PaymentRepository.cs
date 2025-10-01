using FIAP.CloudGames.Core.Data;
using FIAP.CloudGames.Payment.Domain.Models;
using Microsoft.EntityFrameworkCore;

using Models = FIAP.CloudGames.Payment.Domain.Models;

namespace FIAP.CloudGames.Payment.Infra.Data.Repository
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly PaymentContext _context;

        public PaymentRepository(PaymentContext context)
        {
            _context = context;
        }

        public IUnitOfWork UnitOfWork => _context;

        public async Task<IEnumerable<Models.Payment>> GetAll()
        {
            return await _context.Payments.Include(x => x.Transactions).AsNoTracking().ToListAsync();
        }

        public async Task<Models.Payment> GetPaymentByOrderId(Guid orderId)
        {
            return await _context.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == orderId);
        }

        public async Task<IEnumerable<Domain.Models.Transaction>> GetTransactionsByOrderId(Guid orderId)
        {
            return await _context.Transactions.AsNoTracking().Where(t => t.Payment.OrderId == orderId).ToListAsync();
        }

        public void AddPayment(Models.Payment payment)
        {
            _context.Payments.Add(payment);
        }

        public void AddTransaction(Models.Transaction transaction)
        {
            _context.Transactions.Add(transaction);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}