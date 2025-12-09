using NLog;

namespace Curl_maui;

public partial class App : Application
{
    public static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public static Exception LogNException(string? msg = null)
    {
        var ex = new Exception(msg);
        Log.Error(ex);
        if (msg is not null)
        { DialogService.ShowAlert("Error", msg); }
        return ex;
    }
    public static void LogNShow(Exception ex)
    {
        Log.Error(ex);
        DialogService.ShowAlert("Error", ex.Message);
    }

    public App(INavigationService navigationService)
    {
        InitializeComponent();
        MainPage = new NavigationPage();
        navigationService.NavigateToMainPage();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState) ?? throw LogNException("Create window failed.");
        window.Title = "Curl MAUI - HTTP Client";
        window.Created += async (e, s) =>
        {
            Log.Info("Window on Created.");
            NavigationPage? nav = MainPage as NavigationPage;
            MainPage p = nav?.RootPage as MainPage ?? throw LogNException("Create MainPage failed.");
            await p.OnCreated().ConfigureAwait(true);
        };
        window.Stopped += (e, s) =>
        {
            Log.Info("Window on Stopped.");
        };
        window.Destroying += async (e, s) =>
        {
            Log.Info("Window on Destroying.");
            NavigationPage? nav = MainPage as NavigationPage;
            MainPage p = nav?.RootPage as MainPage ?? throw LogNException("Destroying CameraHmiPage failed.");
            await p.OnDestroy().ConfigureAwait(true);
        };
        return window;
    }
}