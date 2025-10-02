using FIAP.CloudGames.Core.Events;
using FIAP.CloudGames.Core.Mediator;
using FIAP.CloudGames.Payment.API.Facade;
using FIAP.CloudGames.Payment.API.Services;
using FIAP.CloudGames.Payment.Domain.Models;
using FIAP.CloudGames.Payment.Infra.Data;
using FIAP.CloudGames.Payment.Infra.Data.EventSourcing;
using FIAP.CloudGames.Payment.Infra.Data.Repository;
using FIAP.CloudGames.Payment.Infra.Data.Repository.EventSourcing;
using FIAP.CloudGames.WebAPI.Core.User;

namespace FIAP.CloudGames.Payment.API.Configuration
{
    public static class DependencyInjectionConfig
    {
        public static void RegisterServices(this IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IAspNetUser, AspNetUser>();

            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IPaymentFacade, PaymentCreditCardFacade>();

            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<PaymentContext>();

            services.AddScoped<IEventStore, SqlEventStore>();
            services.AddScoped<IEventStoreRepository, EventStoreSqlRepository>();

            services.AddScoped<IMediatorHandler, MediatorHandler>();
        }
    }
}