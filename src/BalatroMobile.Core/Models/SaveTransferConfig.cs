namespace BalatroMobile.Core.Models;

public record SaveTransferConfig
{
    public TransferDirection Direction { get; init; } = TransferDirection.PcToAndroid;
    public string? SourcePath { get; init; } // PC save path (auto-detected if null)
    public string TargetAppPackage { get; init; } = "com.unofficial.balatro";
    public IEnumerable<string> FilesToTransfer { get; init; } = new[]
    {
        "settings.jkr",
        "1/profile.jkr",
        "1/meta.jkr",
        "1/save.jkr",
        "2/profile.jkr",
        "2/meta.jkr",
        "2/save.jkr",
        "3/profile.jkr",
        "3/meta.jkr",
        "3/save.jkr"
    };
    public bool CreateBackup { get; init; } = true;
    public bool IncludeMods { get; init; } = true;
    public IEnumerable<string> ExcludedFiles { get; init; } = Array.Empty<string>();
}

public enum TransferDirection
{
    PcToAndroid,
    AndroidToPc
}

public record SaveTransferResult
{
    public bool Success { get; init; }
    public TransferDirection Direction { get; init; }
    public int FilesTransferred { get; init; }
    public long BytesTransferred { get; init; }
    public string? BackupPath { get; init; }
    public IEnumerable<string> TransferredFiles { get; init; } = Array.Empty<string>();
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();
    public TimeSpan Duration { get; init; }
}