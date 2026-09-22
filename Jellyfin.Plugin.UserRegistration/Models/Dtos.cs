using System;

namespace Jellyfin.Plugin.UserRegistration.Models;

/// <summary>
/// Corpo enviado pela tela pública de cadastro.
/// </summary>
public class RegisterRequestDto
{
    /// <summary>
    /// Gets or sets o nome de usuário desejado.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets a senha desejada.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets a confirmação da senha.
    /// </summary>
    public string? ConfirmPassword { get; set; }

    /// <summary>
    /// Gets or sets o código de convite, quando exigido pelo administrador.
    /// </summary>
    public string? InviteCode { get; set; }

    /// <summary>
    /// Gets or sets um recado opcional para o administrador.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Informações públicas usadas para montar a tela de cadastro.
/// </summary>
public class RegistrationStatusDto
{
    /// <summary>
    /// Gets or sets a value indicating whether o cadastro está aberto.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether é preciso informar um código de convite.
    /// </summary>
    public bool RequiresInviteCode { get; set; }

    /// <summary>
    /// Gets or sets o tamanho mínimo da senha.
    /// </summary>
    public int MinimumPasswordLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether o solicitante pode enviar um recado.
    /// </summary>
    public bool AllowMessage { get; set; }

    /// <summary>
    /// Gets or sets o título exibido na tela de cadastro.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets o texto de boas-vindas exibido na tela de cadastro.
    /// </summary>
    public string WelcomeMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a mensagem mostrada após o envio da solicitação.
    /// </summary>
    public string SuccessMessage { get; set; } = string.Empty;
}

/// <summary>
/// Resposta do envio de uma solicitação de cadastro.
/// </summary>
public class RegisterResultDto
{
    /// <summary>
    /// Gets or sets o identificador da solicitação criada.
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// Gets or sets a mensagem a ser exibida ao solicitante.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Solicitação como exibida no painel do administrador.
/// </summary>
public class RegistrationRequestDto
{
    /// <summary>
    /// Gets or sets o identificador da solicitação.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets o identificador do usuário criado.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets o nome de usuário solicitado.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a data/hora (UTC) da solicitação.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Gets or sets a data/hora (UTC) da decisão.
    /// </summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>
    /// Gets or sets quem decidiu.
    /// </summary>
    public string? DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets a situação da solicitação.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets o endereço de origem.
    /// </summary>
    public string? RemoteAddress { get; set; }

    /// <summary>
    /// Gets or sets o recado enviado pelo solicitante.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a conta ainda existe no servidor.
    /// </summary>
    public bool AccountExists { get; set; }
}

/// <summary>
/// Um usuário do servidor, para escolha do perfil modelo no painel.
/// </summary>
public class SimpleUserDto
{
    /// <summary>
    /// Gets or sets o identificador do usuário.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets o nome do usuário.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
