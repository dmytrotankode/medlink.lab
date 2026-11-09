// Реєстрація інтеграції з ЕСОЗ через проксі MedLink: режими Mock | MedLinkProxy | Replay | Live (застарілий), клієнт, outbox-рушій і воркер. У Program.cs — один рядок: builder.AddEhealthIntegration();
using System.Net.Http;

namespace MedLink.LIS.Api.Services.Ehealth;

public static class EhealthServiceCollectionExtensions
{
    /// <summary>Читає секцію Ehealth, реєструє HTTP-клієнт відповідно до режиму та всі сервіси інтеграції (включно з outbox).</summary>
    public static WebApplicationBuilder AddEhealthIntegration(this WebApplicationBuilder builder)
    {
        var s = builder.Services;
        // Налаштування читаються під час створення сервісів, а не тут: так секцію Ehealth можна перевизначити конфігурацією хоста (тести, середовище)
        s.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection("Ehealth").Get<EhealthOptions>() ?? new EhealthOptions());
        s.AddSingleton(sp => new EhealthMockState { JobPendingPolls = Math.Max(0, sp.GetRequiredService<EhealthOptions>().Mock.JobPendingPolls) });
        s.AddSingleton<EhealthReplayState>();
        s.AddTransient<EhealthMockHandler>();
        s.AddTransient<EhealthReplayHandler>();

        s.AddHttpClient(EhealthClient.HttpClientName, (sp, c) =>
            {
                var o = sp.GetRequiredService<EhealthOptions>();
                // MedLinkProxy — адреса проксі; Mock/Replay — умовна адреса двійника (запити не виходять у мережу); Live (застарілий) — прямий ЕСОЗ
                var baseUrl = o.IsLive ? o.BaseUrl : o.IsProxy ? (o.Proxy.BaseUrl ?? "") : "https://ehealth-proxy.mock/";
                if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "https://ehealth-proxy.not-configured.invalid/"; // MedLinkProxy без Ehealth:Proxy:BaseUrl: перший виклик дасть помилку підключення
                c.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
                c.Timeout = Timeout.InfiniteTimeSpan; // таймаут запиту задає клієнт (Ehealth:Http:TimeoutSeconds)
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var o = sp.GetRequiredService<EhealthOptions>();
                if (o.IsReal) return new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(10) }; // 303 у job — не перенаправлення
                return o.IsReplay ? sp.GetRequiredService<EhealthReplayHandler>() : sp.GetRequiredService<EhealthMockHandler>();
            });

        s.AddSingleton<IEhealthTokenProvider, StaticEhealthTokenProvider>();
        s.AddSingleton<IEhealthExchangeLog, DbEhealthExchangeLog>();
        s.AddSingleton<EhealthIdentity>();
        s.AddScoped<IEhealthClient, EhealthClient>();
        s.AddScoped<Services.IEhealthReferralGateway, Services.EhealthReferralGateway>();
        s.AddScoped<EhealthPackageBuilder>();
        s.AddScoped<EhealthSignService>();
        s.AddEhealthOutbox();
        return builder;
    }

    /// <summary>Черга відправки в ЕСОЗ: сервіс постановки, рушій (singleton) і фоновий воркер.</summary>
    public static IServiceCollection AddEhealthOutbox(this IServiceCollection s)
    {
        s.AddScoped<EhealthOutboxService>();
        s.AddSingleton<EhealthOutboxEngine>();
        s.AddHostedService<EhealthOutboxWorker>();
        return s;
    }
}
