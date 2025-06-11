using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace Curl_maui
{
    public static class MauiProgram
    {
#if WINDOWS
        public static MauiApp CreateMauiApp(Action<Microsoft.UI.Xaml.Window> maxWindow)
#else
        public static MauiApp CreateMauiApp()
#endif
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitMediaElement()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
#if WINDOWS
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddWindows(wndLifeCycleBuilder =>
                {
                    wndLifeCycleBuilder
                    .OnWindowCreated(window =>
                    {
                        App.Log.Info("Lifecycle event: MauiApp Started");
                        maxWindow?.Invoke(window);
                    })
                    .OnClosed((window, args) =>
                    {
                        App.Log.Info("Lifecycle event: MauiApp Closed");
                    });

                });
            });
#endif
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddTransient<MainPageVm>();
            builder.Services.AddTransient<MainPage>();
            return builder.Build();
        }
    }
}
