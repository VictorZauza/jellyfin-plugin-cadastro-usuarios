using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserRegistration.Configuration;
using Jellyfin.Plugin.UserRegistration.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Activity;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserRegistration.Services;

/// <summary>
/// Avisa o administrador quando alguém pede acesso ao servidor.
/// </summary>
/// <remarks>
/// Dois destinos independentes: uma linha em Painel → Atividade do próprio
/// Jellyfin, e um webhook (Discord, Slack, ntfy, Gotify, JSON ou um corpo
/// escrito à mão). Nada aqui pode derrubar o cadastro: toda falha é registrada
/// no log e engolida.
/// </remarks>
public class NotificationService
{
    private readonly IActivityManager _activityManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="activityManager">Registro de atividades do Jellyfin.</param>
    /// <param name="httpClientFactory">Fábrica de clientes HTTP do servidor.</param>
    /// <param name="logger">Logger.</param>
    public NotificationService(
        IActivityManager activityManager,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationService> logger)
    {
        _activityManager = activityManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static PluginConfiguration? Config => Plugin.Instance?.Configuration;

    /// <summary>
    /// Avisa que chegou uma nova solicitação. Retorna na hora: o envio acontece
    /// em segundo plano para o visitante não ficar esperando o webhook responder.
    /// </summary>
    /// <param name="entry">A solicitação recém-criada.</param>
    /// <param name="pendingCount">Quantas solicitações estão aguardando decisão.</param>
    public void QueueNewRequest(RegistrationRequest entry, int pendingCount)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var config = Config;
        if (config is null || !config.NotifyOnNewRequest)
        {
            return;
        }

        var fields = BuildFields(entry, "Nova solicitação de cadastro", null, pendingCount);
        fields["texto"] = string.Format(
            CultureInfo.InvariantCulture,
            "{0} pediu acesso ao servidor.\nQuando: {1}\nEndereço: {2}{3}\nAguardando aprovação: {4}",
            entry.Username,
            fields["data"],
            fields["ip"],
            string.IsNullOrEmpty(fields["recado"]) ? string.Empty : "\nRecado: " + fields["recado"],
            pendingCount.ToString(CultureInfo.InvariantCulture));

        Dispatch(config, fields, entry.UserId, "UserRegistrationRequested");
    }

    /// <summary>
    /// Avisa que uma solicitação foi aprovada ou recusada.
    /// </summary>
    /// <param name="entry">A solicitação decidida.</param>
    /// <param name="approved">Verdadeiro para aprovada, falso para recusada.</param>
    /// <param name="pendingCount">Quantas solicitações continuam aguardando.</param>
    public void QueueDecision(RegistrationRequest entry, bool approved, int pendingCount)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var config = Config;
        if (config is null || !config.NotifyOnDecision)
        {
            return;
        }

        var acao = approved ? "Cadastro aprovado" : "Cadastro recusado";
        var fields = BuildFields(entry, acao, entry.DecidedBy, pendingCount);
        fields["texto"] = string.Format(
            CultureInfo.InvariantCulture,
            "{0}: {1}\nPor: {2}\nQuando: {3}\nAguardando aprovação: {4}",
            acao,
            entry.Username,
            fields["admin"],
            fields["data"],
            pendingCount.ToString(CultureInfo.InvariantCulture));

        Dispatch(
            config,
            fields,
            entry.UserId,
            approved ? "UserRegistrationApproved" : "UserRegistrationRejected");
    }

    /// <summary>
    /// Envia um aviso de teste e devolve o que aconteceu, para o painel mostrar.
    /// </summary>
    /// <returns>Uma frase descrevendo o resultado.</returns>
    public async Task<string> SendTestAsync()
    {
        var config = Config;
        if (config is null)
        {
            return "O plugin não está inicializado.";
        }

        if (string.IsNullOrWhiteSpace(config.WebhookUrl))
        {
            return "Nenhum webhook configurado. Preencha o endereço e salve antes de testar.";
        }

        var fake = new RegistrationRequest
        {
            Username = "usuario-de-teste",
            RequestedAt = DateTime.UtcNow,
            RemoteAddress = "192.0.2.10",
            Message = "Este é um aviso de teste enviado pelo painel do plugin."
        };

        var fields = BuildFields(fake, "Aviso de teste", null, 1);
        fields["texto"] =
            "Aviso de teste do plugin Cadastro de Usuários.\nSe você está lendo isto, o webhook está funcionando.";

        return await SendWebhookAsync(config, fields).ConfigureAwait(false);
    }

    private static Dictionary<string, string> BuildFields(
        RegistrationRequest entry,
        string acao,
        string? admin,
        int pendingCount)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["usuario"] = entry.Username ?? string.Empty,
            ["recado"] = entry.Message ?? string.Empty,
            ["ip"] = string.IsNullOrWhiteSpace(entry.RemoteAddress) ? "desconhecido" : entry.RemoteAddress,
            ["data"] = entry.RequestedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            ["pendentes"] = pendingCount.ToString(CultureInfo.InvariantCulture),
            ["id"] = entry.Id.ToString(),
            ["acao"] = acao,
            ["admin"] = string.IsNullOrWhiteSpace(admin) ? "administrador" : admin,
            ["texto"] = string.Empty
        };
    }

    /// <summary>
    /// Manda para os dois destinos sem bloquear quem chamou.
    /// </summary>
    private void Dispatch(
        PluginConfiguration config,
        Dictionary<string, string> fields,
        Guid userId,
        string activityType)
    {
        _ = Task.Run(async () =>
        {
            if (config.LogToActivity)
            {
                await WriteActivityAsync(fields, userId, activityType).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(config.WebhookUrl))
            {
                var result = await SendWebhookAsync(config, fields).ConfigureAwait(false);
                _logger.LogInformation("Aviso de cadastro: {Result}", result);
            }
        });
    }

    private async Task WriteActivityAsync(Dictionary<string, string> fields, Guid userId, string activityType)
    {
        try
        {
            var entry = new ActivityLog(
                string.Format(CultureInfo.InvariantCulture, "{0}: {1}", fields["acao"], fields["usuario"]),
                activityType,
                userId)
            {
                Overview = fields["texto"],
                ShortOverview = string.IsNullOrEmpty(fields["recado"]) ? null : fields["recado"],
                LogSeverity = LogLevel.Information
            };

            await _activityManager.CreateAsync(entry).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Não foi possível registrar o aviso em Painel → Atividade.");
        }
#pragma warning restore CA1031
    }

    private async Task<string> SendWebhookAsync(PluginConfiguration config, Dictionary<string, string> fields)
    {
        if (!Uri.TryCreate(config.WebhookUrl?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "O endereço do webhook não é uma URL http(s) válida.";
        }

        var (body, contentType) = BuildBody(config, fields);

        try
        {
            var seconds = config.WebhookTimeoutSeconds > 0 ? config.WebhookTimeoutSeconds : 10;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Min(seconds, 60)));

            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType)
            };

            ApplyHeaders(request, config.WebhookHeaders);

            var client = _httpClientFactory.CreateClient(NamedClient.Default);
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "enviado com sucesso (HTTP {0}).",
                    (int)response.StatusCode);
            }

            var detail = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            if (detail.Length > 300)
            {
                detail = detail.Substring(0, 300);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "o destino recusou (HTTP {0}). Resposta: {1}",
                (int)response.StatusCode,
                detail);
        }
        catch (OperationCanceledException)
        {
            return "o destino não respondeu dentro do tempo limite.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha ao enviar o webhook de cadastro.");
            return "não foi possível alcançar o destino: " + ex.Message;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao enviar o webhook de cadastro.");
            return "erro inesperado: " + ex.Message;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Monta o corpo conforme o formato escolhido. Os formatos prontos são
    /// serializados pelo System.Text.Json, então não há escape manual; só o
    /// modelo escrito à mão precisa dos valores escapados.
    /// </summary>
    private static (string Body, string ContentType) BuildBody(
        PluginConfiguration config,
        Dictionary<string, string> fields)
    {
        var titulo = fields["acao"];
        var texto = fields["texto"];

        switch ((config.WebhookFormat ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "SLACK":
                return (JsonSerializer.Serialize(new { text = titulo + "\n" + texto }), "application/json");

            case "NTFY":
                // ntfy usa o corpo cru como mensagem. O título fica na primeira
                // linha em vez de um cabeçalho X-Title, porque cabeçalho HTTP com
                // acento não viaja de forma confiável.
                return (titulo + "\n" + texto, "text/plain");

            case "GOTIFY":
                return (
                    JsonSerializer.Serialize(new { title = titulo, message = texto, priority = 5 }),
                    "application/json");

            case "JSON":
                return (
                    JsonSerializer.Serialize(new
                    {
                        evento = fields["acao"],
                        usuario = fields["usuario"],
                        recado = fields["recado"],
                        endereco = fields["ip"],
                        data = fields["data"],
                        pendentes = fields["pendentes"],
                        solicitacao = fields["id"],
                        administrador = fields["admin"],
                        texto
                    }),
                    "application/json");

            case "CUSTOM":
                var contentType = string.IsNullOrWhiteSpace(config.WebhookContentType)
                    ? "application/json"
                    : config.WebhookContentType.Trim();
                var escapar = contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
                return (Fill(config.WebhookBodyTemplate ?? string.Empty, fields, escapar), contentType);

            default:
                // Discord é o padrão.
                return (
                    JsonSerializer.Serialize(new { content = titulo + "\n" + texto }),
                    "application/json");
        }
    }

    /// <summary>
    /// Troca os marcadores {campo} pelos valores, escapando para JSON quando
    /// o corpo é JSON — senão um recado com aspas ou quebra de linha inutiliza
    /// o pedido inteiro.
    /// </summary>
    private static string Fill(string template, Dictionary<string, string> fields, bool jsonEscape)
    {
        var result = new StringBuilder(template);
        foreach (var pair in fields)
        {
            var value = pair.Value ?? string.Empty;
            if (jsonEscape)
            {
                var quoted = JsonSerializer.Serialize(value);
                value = quoted.Substring(1, quoted.Length - 2);
            }

            result.Replace("{" + pair.Key + "}", value);
        }

        return result.ToString();
    }

    private static void ApplyHeaders(HttpRequestMessage request, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var separator = trimmed.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = trimmed.Substring(0, separator).Trim();
            var value = trimmed.Substring(separator + 1).Trim();
            if (name.Length == 0)
            {
                continue;
            }

            // Cabeçalhos de conteúdo (Content-Type e afins) não entram na
            // coleção do pedido; nesse caso vão para o conteúdo.
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }
    }
}
