namespace BalatroMobile.Core.Models;

public record PatchConfig
{
    public string FilePath { get; init; } = string.Empty;
    public string SearchPattern { get; init; } = string.Empty;
    public string Replacement { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsRequired { get; init; } = true;
}

public record PatchResult
{
    public bool Success { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Error { get; init; }
}