using Ganss.Xss;
using Nostrid.Data;

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

        builder.Services.AddSingleton<FeedService>();
        builder.Services.AddSingleton(new EventDatabase(DbConstants.DatabasePath));
        builder.Services.AddSingleton<RelayService>();
        builder.Services.AddSingleton<AccountService>();
        builder.Services.AddSingleton<HtmlSanitizer>();
        builder.Services.AddSingleton<NoteProcessor>();
        builder.Services.AddSingleton<Nip05Service>();

        return builder.Build();
    }

    public static class DbConstants
    {
        public const string DatabaseFilename = "Nostr.db";

        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
    }
}
