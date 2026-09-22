using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserRegistration.Services;

/// <summary>
/// Insere (ou remove) a tag de script do plugin no index.html do cliente web,
/// para que o formulário de cadastro apareça dentro da própria tela de login.
/// </summary>
/// <remarks>
/// É a mesma técnica usada por plugins como Intro Skipper e Jellyscrub. A marcação é
/// refeita a cada inicialização do servidor, então continua valendo depois de uma
/// atualização do Jellyfin, que reescreve o index.html.
/// </remarks>
public class LoginPageInjector : IHostedService
{
    private const string Marker = "jellyfin-plugin-userregistration";

    private static readonly Regex _existingTag = new Regex(
        @"\s*<script[^>]*" + Marker + @"[^>]*></script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IApplicationPaths _paths;
    private readonly ILogger<LoginPageInjector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginPageInjector"/> class.
    /// </summary>
    /// <param name="paths">Caminhos da aplicação.</param>
    /// <param name="logger">Logger.</param>
    public LoginPageInjector(IApplicationPaths paths, ILogger<LoginPageInjector> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is not null)
        {
            // Refaz a marcação quando o administrador liga/desliga a opção no painel.
            plugin.ConfigurationChanged += (_, _) => Apply();
        }

        Apply();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Aplica ou remove a tag conforme a configuração do plugin.
    /// </summary>
    public void Apply()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var wanted = plugin.Configuration.InjectLoginForm;
        var indexPath = Path.Combine(_paths.WebPath ?? string.Empty, "index.html");

        try
        {
            if (string.IsNullOrEmpty(_paths.WebPath) || !File.Exists(indexPath))
            {
                _logger.LogInformation(
                    "index.html do cliente web não encontrado em {Path}; o formulário na tela de login não será instalado.",
                    indexPath);
                return;
            }

            var original = File.ReadAllText(indexPath);
            var cleaned = _existingTag.Replace(original, string.Empty);

            string updated;
            if (wanted)
            {
                var version = plugin.Version?.ToString() ?? "1.0.0.0";
                var tag = string.Concat(
                    "<script id=\"",
                    Marker,
                    "\" defer src=\"configurationpage?name=UserRegistration.js&v=",
                    version,
                    "\"></script>");

                var closeHead = cleaned.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                if (closeHead < 0)
                {
                    _logger.LogWarning("index.html sem </head>; o formulário na tela de login não será instalado.");
                    return;
                }

                updated = cleaned.Insert(closeHead, tag);
            }
            else
            {
                updated = cleaned;
            }

            if (string.Equals(updated, original, StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(indexPath, updated);
            _logger.LogInformation(
                wanted
                    ? "Formulário de cadastro instalado na tela de login ({Path})."
                    : "Formulário de cadastro removido da tela de login ({Path}).",
                indexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Sem permissão de escrita em {Path}. O formulário na tela de login não pôde ser instalado; a tela de cadastro pelo link continua funcionando.",
                indexPath);
        }
    }
}
