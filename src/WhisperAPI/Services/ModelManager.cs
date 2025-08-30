using Microsoft.Extensions.Options;
using Whisper.net;
using Whisper.net.Ggml;
using WhisperAPI.Configuration;

namespace WhisperAPI.Services;

public sealed class ModelManager
{
    private readonly AppConfiguration _config;
    private readonly ILogger<ModelManager> _logger;

    public ModelManager(IOptions<AppConfiguration> options, ILogger<ModelManager> logger)
    {
        _config = options.Value;
        _logger = logger;
    }

    public string GetModelsDirectory()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "models");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetModelPath(string modelName)
    {
        var normalized = NormalizeModelName(modelName);
        var fileName = normalized + ".bin";
        return Path.Combine(GetModelsDirectory(), fileName);
    }

    public async Task<string> EnsureModelAsync(string modelName, CancellationToken ct = default)
    {
        var path = GetModelPath(modelName);
        var normalized = NormalizeModelName(modelName);
        Console.WriteLine($"[model] requested='{modelName}', normalized='{normalized}'");
        Console.WriteLine($"[model] path='{path}'");
        if (File.Exists(path))
        {
            _logger.LogInformation("Model already present: {Path}", path);
            return path;
        }

        _logger.LogInformation("Model not found locally. Will download: {Model}", modelName);
        Console.WriteLine($"[model] downloading '{normalized}'...");
        await DownloadModelAsync(modelName, path, ct);
        Console.WriteLine($"[model] download complete -> '{path}' ({(new FileInfo(path).Exists ? new FileInfo(path).Length : 0)} bytes)");
        return path;
    }

    private static string NormalizeModelName(string modelName)
    {
        // Accept aliases and normalize to OpenAI-like ids
        var id = modelName.Trim().ToLowerInvariant();
        return id switch
        {
            "tiny" or "whisper-tiny" => "whisper-tiny",
            "base" or "whisper-base" => "whisper-base",
            "small" or "whisper-small" => "whisper-small",
            "medium" or "whisper-medium" => "whisper-medium",
            "large-v3" or "whisper-large-v3" or "whisper-v3" or "whisper_v3" => "whisper-large-v3",
            _ => id
        };
    }

    private async Task DownloadModelAsync(string modelName, string destinationPath, CancellationToken ct)
    {
        var normalized = NormalizeModelName(modelName);
        var ggmlType = MapToGgmlType(normalized);
        if (ggmlType is null)
        {
            throw new InvalidOperationException($"Unsupported model name: {modelName}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var tempPath = destinationPath + ".downloading";
        try
        {
            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType.Value, cancellationToken: ct);
            await SaveStreamWithProgressAsync(modelStream, tempPath, ct);
            File.Move(tempPath, destinationPath, overwrite: true);
            _logger.LogInformation("Model download complete: {Path}", destinationPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model download failed for {Model}", modelName);
            Console.Error.WriteLine($"[model] download failed: {ex.GetType().Name}: {ex.Message}");
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }

    private static GgmlType? MapToGgmlType(string normalized)
    {
        return normalized switch
        {
            "whisper-tiny" => GgmlType.Tiny,
            "whisper-tiny.en" or "whisper-tiny-en" => GgmlType.TinyEn,
            "whisper-base" => GgmlType.Base,
            "whisper-base.en" or "whisper-base-en" => GgmlType.BaseEn,
            "whisper-small" => GgmlType.Small,
            "whisper-small.en" or "whisper-small-en" => GgmlType.SmallEn,
            "whisper-medium" => GgmlType.Medium,
            "whisper-medium.en" or "whisper-medium-en" => GgmlType.MediumEn,
            "whisper-large-v1" => GgmlType.LargeV1,
            "whisper-large-v2" => GgmlType.LargeV2,
            "whisper-large-v3" or "whisper-v3" => GgmlType.LargeV3,
            _ => null
        };
    }

    private async Task SaveStreamWithProgressAsync(Stream input, string destinationPath, CancellationToken ct)
    {
        const int bufferSize = 1024 * 1024; // 1MB
        var buffer = new byte[bufferSize];
        long total = 0;
        long lastLog = 0;
        await using var fs = File.Create(destinationPath);
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0) break;
            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
            if (total - lastLog >= 25L * 1024 * 1024)
            {
                lastLog = total;
                _logger.LogInformation("Downloading model... {MB} MB", total / (1024 * 1024));
            }
        }
        await fs.FlushAsync(ct);
    }
}
