namespace WayniBcra.Domain;

/// <summary>
/// Technical lifecycle status of a file import.
/// </summary>
public enum ImportStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
