using Ganss.Xss;
using Microsoft.Extensions.DependencyInjection;
using NNostr.Client;
using Nostrid.Data;
using Nostrid.Data.Relays;
using Nostrid.Externals;
using Nostrid.Interfaces;
using Nostrid.Model;
using Photino.Blazor;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Nostrid.Photino
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

            builder.Services
                .AddLogging();

            // register root component and selector
            builder.RootComponents.Add<Main>("#app");

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


            // customize window
            app.MainWindow
                .SetIconFile("favicon.ico")
                .SetTitle("Nostrid");

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.OpenAlertWindow("Fatal exception", error.ExceptionObject.ToString());
            };

            app.Run();
        }
    }
}
