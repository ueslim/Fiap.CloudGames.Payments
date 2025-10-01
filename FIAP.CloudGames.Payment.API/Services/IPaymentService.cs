using FIAP.CloudGames.Core.Messages.Integration;

namespace FIAP.CloudGames.Payment.API.Services
{
    public interface IPaymentService
    {
        Task<ResponseMessage> AuthorizePayment(Domain.Models.Payment payment);
    }
}