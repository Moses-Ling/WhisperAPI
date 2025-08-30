using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using WhisperAPI.Configuration;

namespace WhisperAPI.Services;

public sealed class StartupInitializer : IHostedService
{
    private readonly ILogger<StartupInitializer> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly AppConfiguration _config;
    private readonly ModelManager _models;

    public StartupInitializer(
        ILogger<StartupInitializer> logger,
        IHostApplicationLifetime lifetime,
        IOptions<AppConfiguration> options,
        ModelManager models)
    {
        _logger = logger;
        _lifetime = lifetime;
        _config = options.Value;
        _models = models;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Ensure model directory and selected model placeholder
            var modelPath = await _models.EnsureModelAsync(_config.Whisper.ModelName, cancellationToken);

            var mode = DetectGpuAvailable() && _config.Performance.EnableGpu ? "GPU" : "CPU";
            _logger.LogInformation("Model ready at {Path}. Inference mode: {Mode}", modelPath, mode);
            Console.WriteLine($"[Whisper] Inference mode: {mode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup initialization failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool DetectGpuAvailable()
    {
        try
        {
            // Heuristic: NVIDIA CUDA driver library presence
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return NativeLibrary.TryLoad("nvcuda", out var handle) && handle != IntPtr.Zero;
            }
        }
        catch
        {
            // ignore
        }
        return false;
    }
}

