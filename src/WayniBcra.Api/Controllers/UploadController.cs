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
    /// POST /upload — registers an import and returns a pre-signed URL to upload the file directly to
    /// S3 (the file does not pass through the API, which is required for the multi-GB BCRA padrón).
    /// The client then does a PUT to <c>uploadUrl</c>; processing is triggered by the S3 event.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Upload([FromBody] UploadRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest("Se requiere el nombre del archivo.");
        }

        var ticket = await _uploadHandler.HandleAsync(request.FileName, cancellationToken);

        return Accepted(new
        {
            importJobId = ticket.ImportJobId,
            uploadUrl = ticket.UploadUrl,
        });
    }

    /// <summary>Request body for <c>POST /upload</c>.</summary>
    public sealed record UploadRequest(string FileName);
}
