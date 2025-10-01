namespace FIAP.CloudGames.Payment.Domain.Models
{
    public enum TransactionStatus
    {
        Authorized = 1,
        Paid,
        Denied,
        Refunded,
        Canceled
    }
}