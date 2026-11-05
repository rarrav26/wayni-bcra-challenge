using Microsoft.AspNetCore.Mvc;
using WayniBcra.Api.Contracts;
using WayniBcra.Application.Queries;

namespace WayniBcra.Api.Controllers;

/// <summary>
/// Debtor query endpoints. Routes follow the challenge statement (Spanish): /deudores.
/// </summary>
[ApiController]
[Route("deudores")]
public sealed class DeudoresController : ControllerBase
{
    private readonly IDebtorReadRepository _debtors;

    public DeudoresController(IDebtorReadRepository debtors)
    {
        _debtors = debtors;
    }

    /// <summary>GET /deudores/{cuit} — returns the debtor by identification number.</summary>
    [HttpGet("{cuit}")]
    public async Task<ActionResult<DebtorResponse>> GetByCuit(string cuit, CancellationToken cancellationToken)
    {
        var debtor = await _debtors.GetByIdentificationNumberAsync(cuit, cancellationToken);
        if (debtor is null)
        {
            return NotFound();
        }

        return Ok(DebtorResponse.FromDomain(debtor));
    }

    /// <summary>GET /deudores/top/{n} — returns the N debtors with the highest total financing.</summary>
    [HttpGet("top/{n:int}")]
    public async Task<ActionResult<IEnumerable<DebtorResponse>>> GetTop(int n, CancellationToken cancellationToken)
    {
        if (n <= 0)
        {
            return BadRequest("La cantidad de deudores debe ser mayor a cero.");
        }

        var debtors = await _debtors.GetTopByTotalFinancingAsync(n, cancellationToken);
        return Ok(debtors.Select(DebtorResponse.FromDomain));
    }
}
