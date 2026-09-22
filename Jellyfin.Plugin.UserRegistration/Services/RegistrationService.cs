using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserRegistration.Configuration;
using Jellyfin.Plugin.UserRegistration.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserRegistration.Services;

/// <summary>
/// Erro de negócio tratado pela API (vira 4xx em vez de 500).
/// </summary>
public class RegistrationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationException"/> class.
    /// </summary>
    /// <param name="statusCode">Código HTTP a devolver.</param>
    /// <param name="message">Mensagem para o usuário.</param>
    public RegistrationException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationException"/> class.
    /// </summary>
    public RegistrationException()
        : this(400, "Erro de cadastro.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationException"/> class.
    /// </summary>
    /// <param name="message">Mensagem para o usuário.</param>
    public RegistrationException(string message)
        : this(400, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationException"/> class.
    /// </summary>
    /// <param name="message">Mensagem para o usuário.</param>
    /// <param name="innerException">Exceção original.</param>
    public RegistrationException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = 400;
    }

    /// <summary>
    /// Gets o código HTTP a devolver.
    /// </summary>
    public int StatusCode { get; } = 400;
}

/// <summary>
/// Regras de cadastro e de aprovação.
/// </summary>
public class RegistrationService
{
    private static readonly Regex _usernameRegex = new Regex(
        @"^[a-zA-Z0-9\-_'.@+]{3,32}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    private static readonly ConcurrentDictionary<string, List<DateTime>> _recentAttempts =
        new ConcurrentDictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

    // O Jellyfin mudou a assinatura de ChangePassword no meio da série 10.11:
    // até a 10.11.5 é (User, senha); da 10.11.6 em diante é (Guid, senha).
    // Chamar por reflexão faz o mesmo binário servir para as duas.
    private static readonly MethodInfo? _changePasswordById =
        typeof(IUserManager).GetMethod("ChangePassword", new[] { typeof(Guid), typeof(string) });

    private static readonly MethodInfo? _changePasswordByUser =
        typeof(IUserManager).GetMethod("ChangePassword", new[] { typeof(User), typeof(string) });

    private readonly IUserManager _userManager;
    private readonly RequestStore _store;
    private readonly ILogger<RegistrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationService"/> class.
    /// </summary>
    /// <param name="userManager">Gerenciador de usuários do Jellyfin.</param>
    /// <param name="store">Armazenamento das solicitações.</param>
    /// <param name="logger">Logger.</param>
    public RegistrationService(IUserManager userManager, RequestStore store, ILogger<RegistrationService> logger)
    {
        _userManager = userManager;
        _store = store;
        _logger = logger;
    }

    private static Plugin PluginInstance =>
        Plugin.Instance ?? throw new InvalidOperationException("Plugin não inicializado.");

    private static PluginConfiguration Config => PluginInstance.Configuration;

    /// <summary>
    /// Informações públicas da tela de cadastro.
    /// </summary>
    /// <returns>O status público.</returns>
    public RegistrationStatusDto GetPublicStatus()
    {
        var config = Config;
        return new RegistrationStatusDto
        {
            Enabled = config.EnableRegistration,
            RequiresInviteCode = !string.IsNullOrWhiteSpace(config.InviteCode),
            MinimumPasswordLength = Math.Max(1, config.MinimumPasswordLength),
            AllowMessage = config.AllowMessage,
            Title = config.PageTitle,
            WelcomeMessage = config.WelcomeMessage,
            SuccessMessage = config.SuccessMessage
        };
    }

    /// <summary>
    /// Registra uma solicitação: cria a conta desativada e a deixa aguardando aprovação.
    /// </summary>
    /// <param name="request">Dados enviados pela tela pública.</param>
    /// <param name="remoteAddress">Endereço IP de origem.</param>
    /// <returns>O resultado do cadastro.</returns>
    public async Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request, string? remoteAddress)
    {
        ArgumentNullException.ThrowIfNull(request);

        var config = Config;

        if (!config.EnableRegistration)
        {
            throw new RegistrationException(403, "O cadastro está fechado no momento.");
        }

        var username = (request.Username ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        var confirm = request.ConfirmPassword ?? string.Empty;

        if (!_usernameRegex.IsMatch(username))
        {
            throw new RegistrationException(
                "O nome de usuário deve ter de 3 a 32 caracteres e usar apenas letras, números e - _ . @ + '.");
        }

        var minLength = Math.Max(1, config.MinimumPasswordLength);
        if (password.Length < minLength)
        {
            throw new RegistrationException(
                string.Format(CultureInfo.InvariantCulture, "A senha precisa ter pelo menos {0} caracteres.", minLength));
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            throw new RegistrationException("As senhas não conferem.");
        }

        if (!string.IsNullOrWhiteSpace(config.InviteCode)
            && !string.Equals((request.InviteCode ?? string.Empty).Trim(), config.InviteCode.Trim(), StringComparison.Ordinal))
        {
            throw new RegistrationException(403, "Código de convite inválido.");
        }

        var message = config.AllowMessage ? Truncate((request.Message ?? string.Empty).Trim(), 280) : null;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            EnforceRateLimit(remoteAddress, config);
            RecordAttempt(remoteAddress);

            var requests = _store.Load();

            var pendingCount = requests.Count(r => r.State == RegistrationRequestState.Pending);
            if (config.MaxPendingRequests > 0 && pendingCount >= config.MaxPendingRequests)
            {
                throw new RegistrationException(
                    429,
                    "Há muitas solicitações aguardando aprovação. Tente novamente mais tarde.");
            }

            if (_userManager.GetUserByName(username) is not null)
            {
                throw new RegistrationException(409, "Este nome de usuário já está em uso.");
            }

            User user;
            try
            {
                user = await _userManager.CreateUserAsync(username).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Nome de usuário recusado pelo servidor: {Username}", username);
                throw new RegistrationException("Este nome de usuário não é aceito pelo servidor.");
            }

            try
            {
                await ChangePasswordAsync(user, password).ConfigureAwait(false);
                await ApplyPendingPolicyAsync(user).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao preparar a conta {Username}; removendo a conta parcial.", username);
                try
                {
                    await _userManager.DeleteUserAsync(user.Id).ConfigureAwait(false);
                }
#pragma warning disable CA1031
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Não foi possível remover a conta parcial {Username}.", username);
                }
#pragma warning restore CA1031

                throw new RegistrationException("Não foi possível concluir o cadastro. Fale com o administrador.");
            }

            var entry = new RegistrationRequest
            {
                UserId = user.Id,
                Username = username,
                RequestedAt = DateTime.UtcNow,
                State = RegistrationRequestState.Pending,
                RemoteAddress = remoteAddress,
                Message = string.IsNullOrWhiteSpace(message) ? null : message
            };

            requests.Add(entry);
            _store.Save(requests);

            _logger.LogInformation(
                "Nova solicitação de cadastro para {Username} (conta criada desativada, aguardando aprovação).",
                username);

            return new RegisterResultDto
            {
                RequestId = entry.Id,
                Message = config.SuccessMessage
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Lista as solicitações para o painel do administrador.
    /// </summary>
    /// <returns>As solicitações, mais recentes primeiro.</returns>
    public IReadOnlyList<RegistrationRequestDto> GetRequests()
    {
        return _store.Load()
            .OrderByDescending(r => r.State == RegistrationRequestState.Pending)
            .ThenByDescending(r => r.RequestedAt)
            .Select(r => new RegistrationRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                Username = r.Username,
                RequestedAt = r.RequestedAt,
                DecidedAt = r.DecidedAt,
                DecidedBy = r.DecidedBy,
                State = r.State.ToString(),
                RemoteAddress = r.RemoteAddress,
                Message = r.Message,
                AccountExists = r.UserId != Guid.Empty && _userManager.GetUserById(r.UserId) is not null
            })
            .ToList();
    }

    /// <summary>
    /// Aprova uma solicitação, liberando a conta para login.
    /// </summary>
    /// <param name="requestId">Identificador da solicitação.</param>
    /// <param name="decidedBy">Nome do administrador.</param>
    /// <returns>Uma tarefa.</returns>
    public async Task ApproveAsync(Guid requestId, string? decidedBy)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var requests = _store.Load();
            var entry = requests.FirstOrDefault(r => r.Id == requestId)
                ?? throw new RegistrationException(404, "Solicitação não encontrada.");

            if (entry.State != RegistrationRequestState.Pending)
            {
                throw new RegistrationException(409, "Esta solicitação já foi decidida.");
            }

            var user = _userManager.GetUserById(entry.UserId)
                ?? throw new RegistrationException(410, "A conta desta solicitação não existe mais no servidor.");

            await ApplyApprovedPolicyAsync(user).ConfigureAwait(false);

            entry.State = RegistrationRequestState.Approved;
            entry.DecidedAt = DateTime.UtcNow;
            entry.DecidedBy = decidedBy;
            _store.Save(requests);

            _logger.LogInformation("Cadastro de {Username} aprovado por {Admin}.", entry.Username, decidedBy ?? "admin");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Recusa uma solicitação e apaga a conta criada.
    /// </summary>
    /// <param name="requestId">Identificador da solicitação.</param>
    /// <param name="decidedBy">Nome do administrador.</param>
    /// <returns>Uma tarefa.</returns>
    public async Task RejectAsync(Guid requestId, string? decidedBy)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var requests = _store.Load();
            var entry = requests.FirstOrDefault(r => r.Id == requestId)
                ?? throw new RegistrationException(404, "Solicitação não encontrada.");

            if (entry.State != RegistrationRequestState.Pending)
            {
                throw new RegistrationException(409, "Esta solicitação já foi decidida.");
            }

            if (entry.UserId != Guid.Empty && _userManager.GetUserById(entry.UserId) is not null)
            {
                await _userManager.DeleteUserAsync(entry.UserId).ConfigureAwait(false);
            }

            entry.State = RegistrationRequestState.Rejected;
            entry.DecidedAt = DateTime.UtcNow;
            entry.DecidedBy = decidedBy;
            _store.Save(requests);

            _logger.LogInformation("Cadastro de {Username} recusado por {Admin}.", entry.Username, decidedBy ?? "admin");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Remove uma solicitação já decidida do histórico.
    /// </summary>
    /// <param name="requestId">Identificador da solicitação.</param>
    /// <returns>Uma tarefa.</returns>
    public async Task DeleteFromHistoryAsync(Guid requestId)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var requests = _store.Load();
            var entry = requests.FirstOrDefault(r => r.Id == requestId)
                ?? throw new RegistrationException(404, "Solicitação não encontrada.");

            if (entry.State == RegistrationRequestState.Pending)
            {
                throw new RegistrationException(409, "Decida a solicitação antes de removê-la do histórico.");
            }

            requests.Remove(entry);
            _store.Save(requests);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Lista os usuários do servidor (para escolher o perfil modelo).
    /// </summary>
    /// <returns>Usuários existentes.</returns>
    public IReadOnlyList<SimpleUserDto> GetServerUsers()
    {
        // Contas desativadas (inclusive as que ainda aguardam aprovação) não servem de modelo.
        return _userManager.Users
            .Where(u => !GetPolicy(u).IsDisabled)
            .Select(u => new SimpleUserDto { Id = u.Id, Name = u.Username })
            .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value.Substring(0, max));

    private static void EnforceRateLimit(string? remoteAddress, PluginConfiguration config)
    {
        if (config.MaxRequestsPerHourPerAddress <= 0 || string.IsNullOrWhiteSpace(remoteAddress))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddHours(-1);
        var attempts = _recentAttempts.GetOrAdd(remoteAddress, _ => new List<DateTime>());
        lock (attempts)
        {
            attempts.RemoveAll(t => t < cutoff);
            if (attempts.Count >= config.MaxRequestsPerHourPerAddress)
            {
                throw new RegistrationException(429, "Muitas tentativas a partir deste endereço. Tente novamente mais tarde.");
            }
        }
    }

    private static void RecordAttempt(string? remoteAddress)
    {
        if (string.IsNullOrWhiteSpace(remoteAddress))
        {
            return;
        }

        var attempts = _recentAttempts.GetOrAdd(remoteAddress, _ => new List<DateTime>());
        lock (attempts)
        {
            attempts.Add(DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Define a senha do usuário, funcionando tanto na assinatura antiga
    /// (User, senha) quanto na nova (Guid, senha) do Jellyfin.
    /// </summary>
    private async Task ChangePasswordAsync(User user, string password)
    {
        var method = _changePasswordById ?? _changePasswordByUser;
        if (method is null)
        {
            throw new RegistrationException(
                500,
                "Esta versão do Jellyfin não é compatível com o plugin (ChangePassword não encontrado).");
        }

        object target = _changePasswordById is not null ? user.Id : user;

        try
        {
            if (method.Invoke(_userManager, new object[] { target, password }) is Task task)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Deixa a exceção original subir, e não o invólucro da reflexão.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    private UserPolicy GetPolicy(User user) =>
        _userManager.GetUserDto(user, null).Policy ?? new UserPolicy();

    private async Task ApplyPendingPolicyAsync(User user)
    {
        var policy = GetPolicy(user);
        policy.IsAdministrator = false;
        policy.IsDisabled = true;
        policy.EnableRemoteAccess = false;
        policy.EnableContentDeletion = false;
        policy.EnableContentDownloading = false;
        policy.EnableUserPreferenceAccess = true;
        await _userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);
    }

    private async Task ApplyApprovedPolicyAsync(User user)
    {
        var config = Config;
        UserPolicy policy;

        var templateUser = config.TemplateUserId != Guid.Empty
            ? _userManager.GetUserById(config.TemplateUserId)
            : null;

        if (templateUser is not null && templateUser.Id != user.Id)
        {
            policy = GetPolicy(templateUser);
            policy.AuthenticationProviderId = GetPolicy(user).AuthenticationProviderId;
            policy.PasswordResetProviderId = GetPolicy(user).PasswordResetProviderId;
        }
        else
        {
            policy = GetPolicy(user);
            policy.EnableAllFolders = config.DefaultEnableAllFolders;
            if (config.DefaultEnableAllFolders)
            {
                policy.EnabledFolders = Array.Empty<Guid>();
            }

            policy.EnableContentDownloading = config.DefaultEnableContentDownloading;
            policy.EnableRemoteAccess = config.DefaultEnableRemoteAccess;
            policy.EnableVideoPlaybackTranscoding = config.DefaultEnableVideoPlaybackTranscoding;
            policy.EnableAudioPlaybackTranscoding = true;
            policy.EnableMediaPlayback = true;
            policy.EnableAllDevices = true;
            policy.MaxActiveSessions = Math.Max(0, config.DefaultMaxActiveSessions);
        }

        // Nunca herda poderes de administrador nem o estado desativado do modelo.
        policy.IsAdministrator = false;
        policy.IsDisabled = false;
        policy.IsHidden = false;
        policy.InvalidLoginAttemptCount = 0;

        await _userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);
    }
}
