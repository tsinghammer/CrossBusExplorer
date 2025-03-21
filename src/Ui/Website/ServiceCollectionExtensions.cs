using CrossBusExplorer.Website.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
namespace CrossBusExplorer.Website;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebsiteServices(this IServiceCollection collection)
    {
        collection.AddScoped<IConnectionsViewModel, ConnectionsViewModel>();
        collection.AddScoped<INavigationViewModel, NavigationViewModel>();
        collection.AddScoped<IQueueViewModel, QueueViewModel>();
        collection.AddScoped<IMessagesViewModel, MessagesViewModel>();
        collection.AddScoped<IJobsViewModel, JobsViewModel>();
        collection.AddScoped<ITopicViewModel, TopicViewModel>();
        collection.AddScoped<ISubscriptionViewModel, SubscriptionViewModel>();
        collection.AddScoped<IRulesViewModel, RulesViewModel>();
        collection.AddCustomTheme();

        return collection.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;

            config.SnackbarConfiguration.PreventDuplicates = false;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 10000;
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
        });
    }

    private static IServiceCollection AddCustomTheme(this IServiceCollection collection)
    {
        collection.AddSingleton(new MudTheme()
        {
            PaletteLight = new PaletteLight
            {
                Secondary = Colors.Blue.Darken2
            },
            PaletteDark = new PaletteDark
            {
                Secondary = Colors.Gray.Darken4
            }
        });

        return collection;
    }
}