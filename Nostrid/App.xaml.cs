using Nostrid.Data;

namespace Nostrid;

public partial class App : Application
{
    private readonly RelayService relayService;
    private readonly FeedService feedService;

    public App(RelayService relayService, FeedService feedService)
	{
		InitializeComponent();

		MainPage = new MainPage();
        this.relayService = relayService;
        this.feedService = feedService;
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        Window window = base.CreateWindow(activationState);

        window.Created += (s, e) =>
        {
            relayService.StartNostrClients();
            feedService.StartQueues();
        };

        window.Destroying += (s, e) =>
        {
            relayService.StopNostrClients();
            feedService.StopQueues();
        };

        return window;
    }
}
