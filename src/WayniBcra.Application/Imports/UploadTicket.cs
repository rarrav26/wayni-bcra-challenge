namespace WayniBcra.Application.Imports;

/// <summary>
/// Result of registering an upload: the created import job id, the storage key the file will live
/// under, and the pre-signed URL the client uses to upload the file directly to storage.
/// </summary>
public sealed record UploadTicket(Guid ImportJobId, string ObjectKey, string UploadUrl);
