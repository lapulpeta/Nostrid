using Ganss.Xss;
using NNostr.Client;
using Nostrid.Data;
using Nostrid.Data.Relays;
using Nostrid.Externals;
using Nostrid.Interfaces;
using Nostrid.Misc;
using Nostrid.Model;
using Plugin.LocalNotification;

namespace Nostrid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

#if ANDROID || IOS
        builder.UseLocalNotification();
#endif

		builder.Services.AddSingleton<FeedService>();
        builder.Services.AddSingleton<EventDatabase>();
        builder.Services.AddSingleton<RelayService>();
        builder.Services.AddSingleton<AccountService>();
        builder.Services.AddSingleton<HtmlSanitizer>();
        builder.Services.AddSingleton<NoteProcessor>();
        builder.Services.AddSingleton<Lud06Service>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<INotificationCounter, NotificationCounter>();
        builder.Services.AddSingleton<ConfigService>();
        builder.Services.AddSingleton<IClipboardService, ClipboardService>();
        builder.Services.AddSingleton<MediaServiceProvider>();
        builder.Services.AddSingleton<IMediaService, NostrBuildMediaService>();
        builder.Services.AddSingleton<IMediaService, VoidCatMediaService>();
        builder.Services.AddSingleton<IMediaService, NostrImgMediaService>();
        builder.Services.AddSingleton<IMediaService, NostrcheckMediaService>();
        builder.Services.AddSingleton<ChannelService>();
        builder.Services.AddSingleton<AllSubscriptionFilterFactory>();
        builder.Services.AddSingleton<DmService>();
        builder.Services.AddSingleton<IAesEncryptor, AesEncryptor>();
        builder.Services.AddSingleton<LocalSignerFactory>();

		var app = builder.Build();

        var eventDatabase = app.Services.GetRequiredService<EventDatabase>();
        eventDatabase.InitDatabase(DbConstants.DatabasePath);

        return app;
    }
}
