using Nostrid.Data;

namespace Nostrid;

public partial class App : Application
{
    private readonly RelayService relayService;
    private readonly FeedService feedService;
    private readonly AccountService accountService;

    public App(RelayService relayService, FeedService feedService, AccountService accountService)
	{
		InitializeComponent();

		MainPage = new MainPage();
        this.relayService = relayService;
        this.feedService = feedService;
        this.accountService = accountService;
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        Window window = base.CreateWindow(activationState);

        window.Created += (s, e) =>
        {
            relayService.StartNostrClients();
            accountService.StartDetailsUpdater();
        };

        window.Destroying += (s, e) =>
        {
            relayService.StopNostrClients();
            accountService.StopDetailsUpdater();
        };

        return window;
    }
}
