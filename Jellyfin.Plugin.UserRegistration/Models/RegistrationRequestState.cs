namespace Jellyfin.Plugin.UserRegistration.Models;

/// <summary>
/// Situação de uma solicitação de cadastro.
/// </summary>
public enum RegistrationRequestState
{
    /// <summary>
    /// Aguardando a confirmação do administrador.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Aprovada pelo administrador, conta liberada para login.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Recusada pelo administrador.
    /// </summary>
    Rejected = 2
}
