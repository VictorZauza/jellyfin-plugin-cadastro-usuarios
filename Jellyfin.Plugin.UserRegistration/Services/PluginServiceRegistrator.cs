using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.UserRegistration.Services;

/// <summary>
/// Registra os serviços do plugin no contêiner de injeção de dependência.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<RequestStore>();
        serviceCollection.AddSingleton<RegistrationService>();
        serviceCollection.AddHostedService<LoginPageInjector>();
    }
}
