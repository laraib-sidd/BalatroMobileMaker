namespace BalatroMobile.Core.Models;

public record BuildConfig
{
    public Platform Platform { get; init; } = Platform.Android;
    public FpsCap FpsCap { get; init; } = FpsCap.Default;
    public bool EnableLandscape { get; init; } = true;
    public bool EnableHighDpi { get; init; } = false;
    public bool DisableCrtShader { get; init; } = false;
    public bool EnableExternalStorage { get; init; } = false;
    public string? CustomFpsValue { get; init; }
    public string OutputPath { get; init; } = "balatro.apk";
    
    // Note: Mods cannot be injected into APK during build.
    // Mods must be transferred via ADB or manually copied after APK installation.
}

public enum Platform
{
    Android
}

public enum FpsCap
{
    Default,    // Use device refresh rate
    Custom,     // Use CustomFpsValue
    None        // No FPS cap
}

public record BuildResult
{
    public bool Success { get; init; }
    public string? OutputPath { get; init; }
    public IEnumerable<string> Messages { get; init; } = Array.Empty<string>();
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();
    public TimeSpan Duration { get; init; }
}