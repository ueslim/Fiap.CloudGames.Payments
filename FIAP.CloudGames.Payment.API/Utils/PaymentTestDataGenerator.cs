using FIAP.CloudGames.Payment.Domain.Models;

namespace FIAP.CloudGames.Payment.API.Utils
{
    public static class PaymentTestDataGenerator
    {
        private static readonly Random _random = new();

        private static string GenerateRandomDigits(int length)
        {
            return string.Join("", Enumerable.Range(0, length).Select(_ => _random.Next(0, 10)));
        }

        public static Domain.Models.Payment GenerateRandomPayment()
        {
            return new Domain.Models.Payment
            {
                OrderId = Guid.NewGuid(),
                Value = _random.Next(50, 500),

                CreditCard = new CreditCard
                {
                    CardNumber = GenerateRandomDigits(16),
                    CardName = $"Test User {_random.Next(1000, 9999)}",
                    ExpirationDate = $"{_random.Next(1, 12):D2}/{_random.Next(DateTime.Now.Year % 100, (DateTime.Now.Year % 100) + 5)}",
                    CVV = GenerateRandomDigits(3)
                },
                PaymentType = PaymentType.CreditCard
            };
        }

        public static TransactionDto MapTransaction(Transaction t) => new TransactionDto(
             Id: t.Id.ToString() ?? string.Empty,
             AuthorizationCode: t.AuthorizationCode,
             CardBrand: t.CardBrand,
             TransactionDate: t.TransactionDate,
             TotalValue: t.TotalValue,
             TransactionCost: t.TransactionCost,
             Status: t.Status.ToString()
         );

        public static PaymentDto MapPayment(Domain.Models.Payment p) => new PaymentDto(
                OrderId: p.OrderId,
                Value: p.Value,
                Transactions: (p.Transactions ?? Enumerable.Empty<Transaction>()).Select(MapTransaction)
            );
    }

    public sealed record TransactionDto(string Id, string AuthorizationCode, string CardBrand, DateTime? TransactionDate, decimal TotalValue, decimal TransactionCost, string Status);

    public sealed record PaymentDto(Guid OrderId, decimal Value, IEnumerable<TransactionDto> Transactions);
}