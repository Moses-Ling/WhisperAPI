namespace WhisperAPI.Configuration;

public sealed class AppConfiguration
{
    public Server Server { get; set; } = new();
    public Whisper Whisper { get; set; } = new();
    public Audio Audio { get; set; } = new();
    public Performance Performance { get; set; } = new();
    public Logging Logging { get; set; } = new();
}

public sealed class Server
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8000;
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class Whisper
{
    public string ModelName { get; set; } = "whisper-large-v3"; // default: Whisper-V3
    public string Language { get; set; } = "en";
    public float Temperature { get; set; } = 0.01f;
    public int ChunkLengthSeconds { get; set; } = 25;
}

public sealed class Audio
{
    public int SampleRate { get; set; } = 16000;
    public int MaxFileSizeMb { get; set; } = 100;
    public bool AutoResample { get; set; } = false;
}

public sealed class Performance
{
    public string Device { get; set; } = "auto"; // auto|cpu|gpu
    public int MaxConcurrentRequests { get; set; } = 4;
    public bool EnableGpu { get; set; } = true;
}

public sealed class Logging
{
    public string Level { get; set; } = "Information";
    public string FilePath { get; set; } = "logs/whisper-server.log";
    public int MaxFileBytes { get; set; } = 10 * 1024 * 1024; // 10 MB
}

