using Ganss.Xss;
using Nostrid.Data;
using Nostrid.Misc;
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
        builder.Services.AddSingleton(new EventDatabase(DbConstants.DatabasePath));
        builder.Services.AddSingleton<RelayService>();
        builder.Services.AddSingleton<AccountService>();
        builder.Services.AddSingleton<HtmlSanitizer>();
        builder.Services.AddSingleton<NoteProcessor>();
        builder.Services.AddSingleton<Nip05Service>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<INotificationCounter, NotificationCounter>();
        builder.Services.AddSingleton<ConfigService>();

		return builder.Build();
    }
}
