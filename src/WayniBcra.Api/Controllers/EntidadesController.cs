using Microsoft.AspNetCore.Mvc;
using WayniBcra.Api.Contracts;
using WayniBcra.Application.Queries;

namespace WayniBcra.Api.Controllers;

/// <summary>
/// Financial entity query endpoints. Routes follow the challenge statement (Spanish): /entidades.
/// </summary>
[ApiController]
[Route("entidades")]
public sealed class EntidadesController : ControllerBase
{
    private readonly IFinancialEntityReadRepository _entities;

    public EntidadesController(IFinancialEntityReadRepository entities)
    {
        _entities = entities;
    }

    /// <summary>GET /entidades/{codigo} — returns the financial entity by code.</summary>
    [HttpGet("{codigo}")]
    public async Task<ActionResult<FinancialEntityResponse>> GetByCodigo(string codigo, CancellationToken cancellationToken)
    {
        var entity = await _entities.GetByEntityCodeAsync(codigo, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        return Ok(FinancialEntityResponse.FromDomain(entity));
    }
}
