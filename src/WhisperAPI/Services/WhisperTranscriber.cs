using Microsoft.Extensions.Options;
using WhisperAPI.Configuration;
using Whisper.net;

namespace WhisperAPI.Services;

public sealed class WhisperTranscriber
{
    private readonly AppConfiguration _config;
    private readonly ModelManager _models;
    private readonly ILogger<WhisperTranscriber> _logger;
    private WhisperFactory? _factory;
    private string? _loadedModelPath;

    public WhisperTranscriber(IOptions<AppConfiguration> options, ModelManager models, ILogger<WhisperTranscriber> logger)
    {
        _config = options.Value;
        _models = models;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var modelPath = await _models.EnsureModelAsync(_config.Whisper.ModelName, ct);
        if (!File.Exists(modelPath) || new FileInfo(modelPath).Length < 1024)
        {
            throw new InvalidOperationException($"Model file is missing or invalid: {modelPath}");
        }

        if (!string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
        {
            _factory?.Dispose();
            _factory = WhisperFactory.FromPath(modelPath);
            _loadedModelPath = modelPath;
            _logger.LogInformation("Whisper model loaded: {Path}", modelPath);
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        if (_factory is null) await InitializeAsync(ct);
        if (_factory is null) throw new InvalidOperationException("Whisper factory not initialized");

        await using var processor = _factory.CreateBuilder()
            .WithLanguage(_config.Whisper.Language ?? "auto")
            .Build();

        await using var fs = File.OpenRead(wavPath);
        var text = new System.Text.StringBuilder();
        var segments = new List<TranscriptionSegment>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int id = 0;
        await foreach (var segment in processor.ProcessAsync(fs, ct))
        {
            text.Append(segment.Text);
            segments.Add(new TranscriptionSegment
            {
                Id = id++,
                Start = segment.Start.TotalSeconds,
                End = segment.End.TotalSeconds,
                Text = segment.Text
            });
        }
        sw.Stop();

        var duration = segments.Count > 0 ? segments[^1].End : 0.0;
        return new TranscriptionResult
        {
            Text = text.ToString().Trim(),
            Duration = duration,
            Language = _config.Whisper.Language,
            Segments = segments
        };
    }

    public sealed class TranscriptionResult
    {
        public string Text { get; set; } = string.Empty;
        public double Duration { get; set; }
        public string Language { get; set; } = "en";
        public List<TranscriptionSegment> Segments { get; set; } = new();
    }

    public sealed class TranscriptionSegment
    {
        public int Id { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
