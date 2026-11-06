namespace WayniBcra.Application.Imports;

/// <summary>
/// Builds and parses the S3 object key for an import. The key embeds the import job id so that the
/// S3 object-created event (which carries only bucket + key) can be correlated back to the job,
/// without an extra lookup. Format: <c>imports/{importJobId}/{fileName}</c>.
/// </summary>
public static class ImportObjectKey
{
    private const string Prefix = "imports";

    public static string Build(Guid importJobId, string fileName) =>
        $"{Prefix}/{importJobId:D}/{fileName}";

    /// <summary>
    /// Extracts the import job id from a key produced by <see cref="Build"/>. Returns false if the
    /// key does not match the expected format.
    /// </summary>
    public static bool TryParseImportJobId(string objectKey, out Guid importJobId)
    {
        importJobId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return false;
        }

        var parts = objectKey.Split('/');
        // Expected: ["imports", "{guid}", "{fileName...}"].
        if (parts.Length < 3 || parts[0] != Prefix)
        {
            return false;
        }

        return Guid.TryParse(parts[1], out importJobId);
    }
}
