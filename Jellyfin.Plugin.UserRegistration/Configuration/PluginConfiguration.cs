using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.UserRegistration.Configuration;

/// <summary>
/// Configuração do plugin de cadastro de usuários.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether a tela pública de cadastro está aberta.
    /// </summary>
    public bool EnableRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets o código de convite. Vazio significa que não é exigido.
    /// </summary>
    public string InviteCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets o tamanho mínimo da senha.
    /// </summary>
    public int MinimumPasswordLength { get; set; } = 6;

    /// <summary>
    /// Gets or sets o número máximo de solicitações pendentes ao mesmo tempo.
    /// </summary>
    public int MaxPendingRequests { get; set; } = 25;

    /// <summary>
    /// Gets or sets quantas solicitações um mesmo endereço IP pode enviar por hora.
    /// </summary>
    public int MaxRequestsPerHourPerAddress { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether o solicitante pode enviar um recado.
    /// </summary>
    public bool AllowMessage { get; set; } = true;

    /// <summary>
    /// Gets or sets o usuário usado como modelo de permissões ao aprovar.
    /// <see cref="Guid.Empty"/> usa as permissões padrão definidas abaixo.
    /// </summary>
    public Guid TemplateUserId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether contas aprovadas veem todas as bibliotecas.
    /// Usado apenas quando não há usuário modelo.
    /// </summary>
    public bool DefaultEnableAllFolders { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether contas aprovadas podem baixar mídias.
    /// Usado apenas quando não há usuário modelo.
    /// </summary>
    public bool DefaultEnableContentDownloading { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether contas aprovadas podem acessar remotamente.
    /// Usado apenas quando não há usuário modelo.
    /// </summary>
    public bool DefaultEnableRemoteAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether contas aprovadas podem transcodificar vídeo.
    /// Usado apenas quando não há usuário modelo.
    /// </summary>
    public bool DefaultEnableVideoPlaybackTranscoding { get; set; } = true;

    /// <summary>
    /// Gets or sets o número máximo de sessões simultâneas (0 = sem limite).
    /// Usado apenas quando não há usuário modelo.
    /// </summary>
    public int DefaultMaxActiveSessions { get; set; }

    /// <summary>
    /// Gets or sets o título exibido na tela pública de cadastro.
    /// </summary>
    public string PageTitle { get; set; } = "Criar conta";

    /// <summary>
    /// Gets or sets o texto de boas-vindas da tela pública de cadastro.
    /// </summary>
    public string WelcomeMessage { get; set; } =
        "Escolha um nome de usuário e uma senha. Seu acesso é liberado assim que o administrador aprovar.";

    /// <summary>
    /// Gets or sets a mensagem exibida após o envio da solicitação.
    /// </summary>
    public string SuccessMessage { get; set; } =
        "Solicitação enviada! Assim que o administrador aprovar, você já poderá entrar com esse usuário e senha.";
}
