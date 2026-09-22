using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserRegistration.Models;
using Jellyfin.Plugin.UserRegistration.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserRegistration.Api;

/// <summary>
/// Endpoints do cadastro público e do painel de aprovação.
/// </summary>
[ApiController]
[Route("UserRegistration")]
public class UserRegistrationController : ControllerBase
{
    private readonly RegistrationService _service;
    private readonly ILogger<UserRegistrationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegistrationController"/> class.
    /// </summary>
    /// <param name="service">Serviço de cadastro.</param>
    /// <param name="logger">Logger.</param>
    public UserRegistrationController(RegistrationService service, ILogger<UserRegistrationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Página pública de cadastro.
    /// </summary>
    /// <returns>O HTML da tela de cadastro.</returns>
    [HttpGet("Page")]
    [AllowAnonymous]
    [Produces(MediaTypeNames.Text.Html)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetRegistrationPage()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        Response.Headers.CacheControl = "no-store";
        return Content(plugin.GetRegistrationPageHtml(), "text/html; charset=utf-8");
    }

    /// <summary>
    /// Informações públicas para montar a tela de cadastro.
    /// </summary>
    /// <returns>O status do cadastro.</returns>
    [HttpGet("Public/Status")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<RegistrationStatusDto> GetPublicStatus() => Ok(_service.GetPublicStatus());

    /// <summary>
    /// Envia uma solicitação de cadastro.
    /// </summary>
    /// <param name="request">Dados do cadastro.</param>
    /// <returns>O resultado da solicitação.</returns>
    [HttpPost("Public/Register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegisterResultDto>> Register([FromBody] RegisterRequestDto request)
    {
        try
        {
            var remote = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _service.RegisterAsync(request, remote).ConfigureAwait(false);
            return Ok(result);
        }
        catch (RegistrationException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar uma solicitação de cadastro.");
            return Problem("Não foi possível concluir o cadastro.", statusCode: StatusCodes.Status500InternalServerError);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Lista as solicitações (somente administrador).
    /// </summary>
    /// <returns>As solicitações.</returns>
    [HttpGet("Requests")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<RegistrationRequestDto>> GetRequests() => Ok(_service.GetRequests());

    /// <summary>
    /// Aprova uma solicitação (somente administrador).
    /// </summary>
    /// <param name="requestId">Identificador da solicitação.</param>
    /// <returns>Sem conteúdo.</returns>
    [HttpPost("Requests/{requestId}/Approve")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Approve([FromRoute] Guid requestId)
    {
        try
        {
            await _service.ApproveAsync(requestId, HttpContext.User.Identity?.Name).ConfigureAwait(false);
            return NoContent();
        }
        catch (RegistrationException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode);
        }
    }

    /// <summary>
    /// Recusa uma solicitação e remove a conta criada (somente administrador).
    /// </summary>
    /// <param name="requestId">Identificador da solicitação.</param>
    /// <returns>Sem conteúdo.</returns>
    [HttpPost("Requests/{requestId}/Reject")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Reject([FromRoute] Guid requestId)
    {
        try
        {
            await _service.RejectAsync(requestId, HttpContext.User.Identity?.Name).ConfigureAwait(false);
            return NoContent();
        }
        catch (RegistrationException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode);
        }
    }

    /// <summary>
    /// Remove uma solicitação já decidida do histórico (somente administrador).
    /// </summary>
    /// <param name="requestId">Identificador da solicitação.</param>
    /// <returns>Sem conteúdo.</returns>
    [HttpDelete("Requests/{requestId}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteFromHistory([FromRoute] Guid requestId)
    {
        try
        {
            await _service.DeleteFromHistoryAsync(requestId).ConfigureAwait(false);
            return NoContent();
        }
        catch (RegistrationException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode);
        }
    }

    /// <summary>
    /// Lista os usuários do servidor para escolha do perfil modelo (somente administrador).
    /// </summary>
    /// <returns>Os usuários.</returns>
    [HttpGet("Users")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SimpleUserDto>> GetServerUsers() => Ok(_service.GetServerUsers());
}
