using System;

namespace Jellyfin.Plugin.UserRegistration.Models;

/// <summary>
/// Uma solicitação de cadastro feita por um visitante.
/// </summary>
/// <remarks>
/// A conta do Jellyfin é criada imediatamente, porém desativada. Nenhuma senha é
/// guardada por este plugin: ela vai direto para o cofre de senhas do Jellyfin.
/// A aprovação do administrador apenas reativa a conta.
/// </remarks>
public class RegistrationRequest
{
    /// <summary>
    /// Gets or sets o identificador da solicitação.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets o identificador do usuário criado (desativado) no Jellyfin.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets o nome de usuário solicitado.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a data/hora (UTC) em que a solicitação foi feita.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets a data/hora (UTC) em que o administrador decidiu.
    /// </summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>
    /// Gets or sets o nome do administrador que decidiu.
    /// </summary>
    public string? DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets a situação da solicitação.
    /// </summary>
    public RegistrationRequestState State { get; set; } = RegistrationRequestState.Pending;

    /// <summary>
    /// Gets or sets o endereço IP de origem da solicitação.
    /// </summary>
    public string? RemoteAddress { get; set; }

    /// <summary>
    /// Gets or sets uma mensagem opcional enviada pelo solicitante.
    /// </summary>
    public string? Message { get; set; }
}
