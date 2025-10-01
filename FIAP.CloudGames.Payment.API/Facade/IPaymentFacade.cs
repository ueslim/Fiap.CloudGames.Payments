using FIAP.CloudGames.Payment.Domain.Models;

namespace FIAP.CloudGames.Payment.API.Facade
{
    public interface IPaymentFacade
    {
        Task<Transaction> AuthorizePayment(Domain.Models.Payment payment);

        Task<Transaction> CapturePayment(Transaction transaction);

        Task<Transaction> CancelAuthorization(Transaction transaction);
    }
}