using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PolarVisualizeDesktopApp.Services;
using Syncfusion.Blazor;
using System.Reflection;

namespace PolarVisualizeDesktopApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
                "Ngo9BigBOggjGyl/Vkd+XU9FcVRDX3xIf0x0RWFcb1Z6dlxMZFxBNQtUQF1hTH9Sd0RiWH5YcHxUR2FVWkd3"
            );

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

            // Add configuration from appsettings.json
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("PolarVisualizeDesktopApp.appsettings.json");

            if (stream != null)
            {
                var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddJsonStream(stream)
                    .Build();

                builder.Configuration.AddConfiguration(config);
            }

            // Add Syncfusion Blazor service
            builder.Services.AddSyncfusionBlazor();

            // Register Polar Services
            builder.Services.AddSingleton<PolarService>();

            return builder.Build();
        }
    }
}