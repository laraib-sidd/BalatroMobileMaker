namespace BalatroMobile.Core.Models;

public record ModInjectionConfig
{
    public string SourceModsPath { get; init; } = string.Empty; // %APPDATA%\Balatro\Mods
    public string TargetModsPath { get; init; } = string.Empty; // Android app data directory
    public bool IncludeLovelyDump { get; init; } = true;
    public bool IncludeSMODS { get; init; } = true;
    public bool IncludeLibraries { get; init; } = true;
    public bool CreateLovelyConfig { get; init; } = true;
    public IEnumerable<string> ExcludedMods { get; init; } = Array.Empty<string>();
}

public record ModInjectionResult
{
    public bool Success { get; init; }
    public string TargetPath { get; init; } = string.Empty;
    public IEnumerable<string> InjectedComponents { get; init; } = Array.Empty<string>();
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();
    public int FilesCopied { get; init; }
    public long BytesCopied { get; init; }
}