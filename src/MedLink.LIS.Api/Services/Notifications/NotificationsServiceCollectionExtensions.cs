// Реєстрація сповіщень пацієнтам і доставки подій у МІС: параметри (секції Notify і MisDelivery), провайдери каналів, відправник, воркери.
namespace MedLink.LIS.Api.Services.Notifications;

public static class NotificationsServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsAndMisDelivery(this IServiceCollection s)
    {
        // Параметри читаються під час створення сервісів (а не тут): секції можна перевизначити конфігурацією хоста (тести, середовище)
        s.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection("Notify").Get<NotifyOptions>() ?? new NotifyOptions());
        s.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection("MisDelivery").Get<MisDeliveryOptions>() ?? new MisDeliveryOptions());
        s.AddHttpClient("notify");
        s.AddHttpClient(MisDeliveryEngine.HttpClientName);

        s.AddSingleton<ISmtpTransport, SystemSmtpTransport>();
        s.AddSingleton<INotifyChannelProvider>(sp => new SmtpEmailProvider(sp.GetRequiredService<NotifyOptions>(), sp.GetRequiredService<ISmtpTransport>()));
        foreach (var channel in new[] { NotifyChannels.Sms, NotifyChannels.Viber, NotifyChannels.Telegram })
        {
            var c = channel;
            s.AddSingleton<INotifyChannelProvider>(sp => new HttpNotifyProvider(c, sp.GetRequiredService<NotifyOptions>().Http(c)!, sp.GetRequiredService<IHttpClientFactory>()));
        }
        s.AddSingleton<NotificationDispatcher>();
        s.AddHostedService<NotificationWorker>();
        s.AddSingleton<MisDeliveryEngine>();
        s.AddHostedService<MisDeliveryWorker>();
        s.AddScoped<NotificationAdminService>();
        return s;
    }
}
