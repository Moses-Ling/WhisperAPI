using NAudio.Wave;

namespace WhisperAPI.Services;

public sealed class AudioValidationService
{
    private readonly ILogger<AudioValidationService> _logger;

    public AudioValidationService(ILogger<AudioValidationService> logger)
    {
        _logger = logger;
    }

    public async Task<string> NormalizeToWav16kMonoAsync(Stream input, string originalFileName, CancellationToken ct)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "whisperapi");
        Directory.CreateDirectory(tempDir);
        var inputPath = Path.Combine(tempDir, Guid.NewGuid() + Path.GetExtension(originalFileName));
        var outputPath = Path.Combine(tempDir, Guid.NewGuid() + ".wav");

        await using (var fs = File.Create(inputPath))
        {
            await input.CopyToAsync(fs, ct);
        }

        using var reader = new AudioFileReader(inputPath);
        var targetFormat = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(reader, targetFormat) { ResamplerQuality = 60 };
        WaveFileWriter.CreateWaveFile(outputPath, resampler);

        try { File.Delete(inputPath); } catch { }
        _logger.LogInformation("Audio normalized to 16kHz mono WAV at {Path}", outputPath);
        return outputPath;
    }
}

