using Microsoft.AspNetCore.Mvc;
using WayniBcra.Application.Imports;

namespace WayniBcra.Api.Controllers;

/// <summary>
/// File import endpoint. Accepts the BCRA file, stores it, registers the import and enqueues it for
/// asynchronous processing by the Worker. Route follows the challenge statement: /upload.
/// </summary>
[ApiController]
[Route("upload")]
public sealed class UploadController : ControllerBase
{
    private readonly UploadImportHandler _uploadHandler;

    public UploadController(UploadImportHandler uploadHandler)
    {
        _uploadHandler = uploadHandler;
    }

    /// <summary>
    /// POST /upload — uploads the BCRA file (multipart/form-data) and starts asynchronous processing.
    /// Returns 202 Accepted with the created import job id.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        await using var stream = file.OpenReadStream();
        var importJobId = await _uploadHandler.HandleAsync(file.FileName, stream, cancellationToken);

        return Accepted(new { importJobId });
    }
}
