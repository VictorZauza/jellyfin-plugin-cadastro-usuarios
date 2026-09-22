using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.UserRegistration.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.UserRegistration;

/// <summary>
/// Plugin de cadastro de usuários com aprovação do administrador.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Caminhos da aplicação.</param>
    /// <param name="xmlSerializer">Serializador XML.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets a instância atual do plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Cadastro de Usuários";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("b4f0a5c2-9d71-4f3a-8f21-6c2e5a7d41b9");

    /// <inheritdoc />
    public override string Description =>
        "Tela pública para o visitante criar uma conta e um painel onde o administrador confirma cada cadastro.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            }
        };
    }

    /// <summary>
    /// Lê a página pública de cadastro embutida no assembly.
    /// </summary>
    /// <returns>O HTML da página.</returns>
    public string GetRegistrationPageHtml()
    {
        var resource = string.Format(
            CultureInfo.InvariantCulture,
            "{0}.Web.register.html",
            GetType().Namespace);

        using var stream = GetType().Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Página de cadastro não encontrada no assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Grava a configuração atual em disco.
    /// </summary>
    public void Save() => SaveConfiguration();
}
