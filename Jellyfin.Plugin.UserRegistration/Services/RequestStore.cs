using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Jellyfin.Plugin.UserRegistration.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserRegistration.Services;

/// <summary>
/// Guarda as solicitações em um arquivo próprio, separado da configuração do plugin.
/// </summary>
/// <remarks>
/// Ficar fora do arquivo de configuração evita que salvar as configurações no painel
/// apague uma solicitação que chegou enquanto a tela estava aberta.
/// </remarks>
public class RequestStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    private readonly ILogger<RequestStore> _logger;
    private readonly object _fileLock = new object();

    private List<RegistrationRequest>? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestStore"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public RequestStore(ILogger<RequestStore> logger)
    {
        _logger = logger;
    }

    private static string FilePath
    {
        get
        {
            var plugin = Plugin.Instance
                ?? throw new InvalidOperationException("Plugin não inicializado.");
            return Path.Combine(plugin.DataFolderPath, "requests.json");
        }
    }

    /// <summary>
    /// Lê as solicitações gravadas.
    /// </summary>
    /// <returns>A lista de solicitações.</returns>
    public List<RegistrationRequest> Load()
    {
        lock (_fileLock)
        {
            if (_cache is not null)
            {
                return CloneList(_cache);
            }

            var path = FilePath;
            if (!File.Exists(path))
            {
                _cache = new List<RegistrationRequest>();
                return new List<RegistrationRequest>();
            }

            try
            {
                var json = File.ReadAllText(path);
                _cache = JsonSerializer.Deserialize<List<RegistrationRequest>>(json, _jsonOptions)
                    ?? new List<RegistrationRequest>();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Não foi possível ler {Path}; começando com a lista vazia.", path);
                _cache = new List<RegistrationRequest>();
            }

            return CloneList(_cache);
        }
    }

    /// <summary>
    /// Grava as solicitações.
    /// </summary>
    /// <param name="requests">Lista a gravar.</param>
    public void Save(List<RegistrationRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        lock (_fileLock)
        {
            var path = FilePath;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(requests, _jsonOptions));
                File.Move(temp, path, true);
                _cache = CloneList(requests);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Não foi possível gravar {Path}.", path);
                throw new RegistrationException(
                    500,
                    "Não foi possível gravar a lista de solicitações no servidor.");
            }
        }
    }

    private static List<RegistrationRequest> CloneList(List<RegistrationRequest> source)
    {
        var copy = new List<RegistrationRequest>(source.Count);
        foreach (var item in source)
        {
            copy.Add(new RegistrationRequest
            {
                Id = item.Id,
                UserId = item.UserId,
                Username = item.Username,
                RequestedAt = item.RequestedAt,
                DecidedAt = item.DecidedAt,
                DecidedBy = item.DecidedBy,
                State = item.State,
                RemoteAddress = item.RemoteAddress,
                Message = item.Message
            });
        }

        return copy;
    }
}
