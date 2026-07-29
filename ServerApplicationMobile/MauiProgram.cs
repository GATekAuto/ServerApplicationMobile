using Microsoft.Maui.Controls.Hosting;
using ServerApplicationMobile.Controls;
using ServerApplicationMobile.Handlers;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiMaps()
			.ConfigureMauiHandlers(handlers =>
			{
				handlers.AddHandler<ClusteredMap, ClusteredMapHandler>();
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("fa_solid.ttf", "FontAwesome");
			});

		// Register database service
		builder.Services.AddSingleton<DatabaseService>();
		builder.Services.AddSingleton<AuthenticationService>();
		builder.Services.AddSingleton<CustomerDataService>();
		builder.Services.AddSingleton<ChatService>();
		builder.Services.AddSingleton<ChatNotificationService>();
		builder.Services.AddSingleton<ChatTranscriptService>();
		
		// Register pages that use DatabaseService
		builder.Services.AddTransient<CustomersPage>();
		builder.Services.AddTransient<ChatPage>();
		builder.Services.AddTransient<CustomerDetailPage>();
		builder.Services.AddTransient<MapPage>();
		builder.Services.AddTransient<AppTabbedPage>();
		builder.Services.AddTransient<DatabaseTestPage>();
		builder.Services.AddTransient<LoginPage>();

        return builder.Build();
	}
}
