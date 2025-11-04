using Microsoft.Extensions.Logging;
using PolarVisualizeDesktopApp.Services;
using Syncfusion.Blazor;

namespace PolarVisualizeDesktopApp
{
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
    		builder.Logging.AddDebug();
#endif

            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
                "Ngo9BigBOggjGyl/Vkd+XU9FcVRDX3xNYVF2R2ZJfl56cVJMZVtBNQtUQF1hTH9SdkFiWHtdcnBURmVZWkd3"
            );

            // Add Syncfusion Blazor service
            builder.Services.AddSyncfusionBlazor();

            // Register Polar Services
            builder.Services.AddSingleton<PolarService>();
            return builder.Build();
        }
    }
}
