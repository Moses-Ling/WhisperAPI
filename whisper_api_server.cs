// Program.cs - Entry Point
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using WhisperAPI.Services;
using WhisperAPI.Models;
using Serilog;

namespace WhisperAPI
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Command line options
            var hostnameOption = new Option<string>("--hostname", () => "localhost", "Server hostname");
            var portOption = new Option<int>("--port", () => 8000, "Server port");
            var modelOption = new Option<string>("--model", () => "base", "Whisper model name");
            var languageOption = new Option<string>("--language", () => "en", "Language (en=English, m=Multi)");
            
            var rootCommand = new RootCommand("Whisper Server - OpenAI Compatible API")
            {
                hostnameOption, portOption, modelOption, languageOption
            };

            rootCommand.SetHandler(async (hostname, port, model, language) =>
            {
                await StartServer(hostname, port, model, language);
            }, hostnameOption, portOption, modelOption, languageOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task StartServer(string hostname, int port, string modelName, string language)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/whisper-server.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                var builder = WebApplication.CreateBuilder();
                
                // Add services
                builder.Services.AddControllers();
                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowAll", policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
                });

                // Configure server settings
                builder.Services.Configure<WhisperServerConfig>(config =>
                {
                    config.Hostname = hostname;
                    config.Port = port;
                    config.ModelName = modelName;
                    config.Language = language;
                    config.ModelsDirectory = "models";
                    config.MaxFileSizeMB = 100;
                    config.ChunkLengthSeconds = 25;
                    config.Temperature = 0.01f;
                    config.ReturnTimestamps = false;
                });

                // Register services
                builder.Services.AddSingleton<ModelManager>();
                builder.Services.AddSingleton<WhisperTranscriber>();
                builder.Services.AddScoped<TranscriptionService>();
                builder.Services.AddScoped<HistoryLogger>();

                builder.Host.UseSerilog();

                var app = builder.Build();

                // Configure pipeline
                app.UseCors("AllowAll");
                app.UseRouting();
                app.MapControllers();
                app.UseStaticFiles();

                // Initialize Whisper model
                var modelManager = app.Services.GetRequiredService<ModelManager>();
                var config = app.Services.GetRequiredService<IConfiguration>();
                await modelManager.EnsureModelAsync(modelName);

                Log.Information($"Starting Whisper Server on {hostname}:{port}");
                await app.RunAsync($"http://{hostname}:{port}");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}

// Models/WhisperServerConfig.cs
namespace WhisperAPI.Models
{
    public class WhisperServerConfig
    {
        public string Hostname { get; set; } = "localhost";
        public int Port { get; set; } = 8000;
        public string ModelName { get; set; } = "base";
        public string Language { get; set; } = "en";
        public string ModelsDirectory { get; set; } = "models";
        public int MaxFileSizeMB { get; set; } = 100;
        public int ChunkLengthSeconds { get; set; } = 25;
        public float Temperature { get; set; } = 0.01f;
        public bool ReturnTimestamps { get; set; } = false;
    }
}

// Models/TranscriptionModels.cs
namespace WhisperAPI.Models
{
    public class TranscriptionRequest
    {
        public IFormFile File { get; set; }
        public string Model { get; set; } = "whisper-1";
        public string Language { get; set; } = "en";
        public float Temperature { get; set; } = 0.0f;
        public string Prompt { get; set; } = "";
        public bool ReturnTimestamps { get; set; } = false;
    }

    public class TranscriptionResponse
    {
        public string Text { get; set; }
        public List<TranscriptionSegment> Segments { get; set; }
        public double ProcessingTime { get; set; }
        public long ResponseSizeBytes { get; set; }
        public double DurationSeconds { get; set; }
        public string Model { get; set; }
    }

    public class TranscriptionSegment
    {
        public int StartTimeMs { get; set; }
        public int EndTimeMs { get; set; }
        public string Text { get; set; }
    }

    public class ModelInfo
    {
        public string Id { get; set; }
        public string Object { get; set; } = "model";
        public string OwnedBy { get; set; } = "openai";
        public List<object> Permissions { get; set; } = new();
    }

    public class ModelsResponse
    {
        public string Object { get; set; } = "list";
        public List<ModelInfo> Data { get; set; } = new();
    }

    public class ErrorResponse
    {
        public string Error { get; set; }
        public string Details { get; set; }
    }
}

// Controllers/ModelsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhisperAPI.Models;

namespace WhisperAPI.Controllers
{
    [ApiController]
    [Route("v1/[controller]")]
    public class ModelsController : ControllerBase
    {
        private readonly WhisperServerConfig _config;

        public ModelsController(IOptions<WhisperServerConfig> config)
        {
            _config = config.Value;
        }

        [HttpGet]
        public IActionResult GetModels()
        {
            var response = new ModelsResponse
            {
                Data = new List<ModelInfo>
                {
                    new ModelInfo
                    {
                        Id = _config.ModelName,
                        Object = "model",
                        OwnedBy = "openai"
                    }
                }
            };

            return Ok(response);
        }

        [HttpGet("{modelId}")]
        public IActionResult GetModel(string modelId)
        {
            if (modelId != _config.ModelName)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "Model not found",
                    Details = $"Model '{modelId}' does not exist"
                });
            }

            var response = new ModelInfo
            {
                Id = modelId,
                Object = "model",
                OwnedBy = "openai"
            };

            return Ok(response);
        }
    }
}

// Controllers/AudioController.cs
using Microsoft.AspNetCore.Mvc;
using WhisperAPI.Models;
using WhisperAPI.Services;
using System.Diagnostics;

namespace WhisperAPI.Controllers
{
    [ApiController]
    [Route("v1/audio")]
    public class AudioController : ControllerBase
    {
        private readonly TranscriptionService _transcriptionService;
        private readonly ILogger<AudioController> _logger;

        public AudioController(TranscriptionService transcriptionService, ILogger<AudioController> logger)
        {
            _transcriptionService = transcriptionService;
            _logger = logger;
        }

        [HttpPost("transcriptions")]
        public async Task<IActionResult> Transcribe([FromForm] TranscriptionRequest request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new ErrorResponse { Error = "No file provided" });
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await _transcriptionService.TranscribeAsync(request);
                stopwatch.Stop();

                result.ProcessingTime = stopwatch.Elapsed.TotalSeconds;
                result.ResponseSizeBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(result).Length;

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transcription");
                return StatusCode(500, new ErrorResponse { Error = ex.Message });
            }
        }

        [HttpPost("transcriptions/url")]
        public async Task<IActionResult> TranscribeFromUrl([FromBody] UrlTranscriptionRequest request)
        {
            if (string.IsNullOrEmpty(request.Url))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "No URL provided",
                    Details = "Please provide 'url' in the JSON request"
                });
            }

            try
            {
                var result = await _transcriptionService.TranscribeFromUrlAsync(request.Url, request.Language, request.Temperature, request.ReturnTimestamps);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during URL transcription");
                return StatusCode(500, new ErrorResponse { Error = ex.Message });
            }
        }

        [HttpPost("transcriptions/base64")]
        public async Task<IActionResult> TranscribeFromBase64([FromBody] Base64TranscriptionRequest request)
        {
            if (string.IsNullOrEmpty(request.File))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "No base64 file provided",
                    Details = "Please provide 'file' in the JSON request"
                });
            }

            try
            {
                var result = await _transcriptionService.TranscribeFromBase64Async(request.File, request.Language, request.Temperature, request.ReturnTimestamps);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during base64 transcription");
                return StatusCode(500, new ErrorResponse { Error = ex.Message });
            }
        }
    }

    public class UrlTranscriptionRequest
    {
        public string Url { get; set; }
        public string Language { get; set; } = "en";
        public float Temperature { get; set; } = 0.0f;
        public bool ReturnTimestamps { get; set; } = false;
    }

    public class Base64TranscriptionRequest
    {
        public string File { get; set; }
        public string Language { get; set; } = "en";
        public float Temperature { get; set; } = 0.0f;
        public bool ReturnTimestamps { get; set; } = false;
    }
}

// Controllers/HealthController.cs
using Microsoft.AspNetCore.Mvc;

namespace WhisperAPI.Controllers
{
    [ApiController]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new { status = "ok", version = "1.0.0" });
        }

        [HttpGet("config")]
        public IActionResult GetConfig([FromServices] IConfiguration config)
        {
            return Ok(config.AsEnumerable().ToDictionary(x => x.Key, x => x.Value));
        }
    }
}

// Services/ModelManager.cs
using Microsoft.Extensions.Options;
using WhisperAPI.Models;
using Whisper.net;
using Whisper.net.Ggml;

namespace WhisperAPI.Services
{
    public class ModelManager
    {
        private readonly WhisperServerConfig _config;
        private readonly ILogger<ModelManager> _logger;

        public ModelManager(IOptions<WhisperServerConfig> config, ILogger<ModelManager> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public async Task<string> EnsureModelAsync(string modelName)
        {
            var modelPath = Path.Combine(_config.ModelsDirectory, $"ggml-{modelName}.bin");
            
            if (File.Exists(modelPath))
            {
                _logger.LogInformation($"Model {modelName} found at {modelPath}");
                return modelPath;
            }

            _logger.LogInformation($"Model {modelName} not found. Starting download...");

            try
            {
                Directory.CreateDirectory(_config.ModelsDirectory);
                
                var ggmlType = MapModelNameToGgmlType(modelName);
                using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
                using var fileWriter = File.OpenWrite(modelPath);
                
                await modelStream.CopyToAsync(fileWriter);
                
                _logger.LogInformation($"Model {modelName} downloaded successfully to {modelPath}");
                return modelPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to download model {modelName}: {ex.Message}", ex);
            }
        }

        private GgmlType MapModelNameToGgmlType(string modelName)
        {
            return modelName.ToLower() switch
            {
                "base" or "whisper-v1" => GgmlType.Base,
                "tiny" => GgmlType.Tiny,
                "small" => GgmlType.Small,
                "medium" => GgmlType.Medium,
                "large" or "large-v3" => GgmlType.LargeV3,
                _ => throw new ArgumentException($"Unknown model name: {modelName}")
            };
        }
    }
}

// Services/WhisperTranscriber.cs
using Microsoft.Extensions.Options;
using WhisperAPI.Models;
using Whisper.net;

namespace WhisperAPI.Services
{
    public class WhisperTranscriber
    {
        private readonly WhisperServerConfig _config;
        private readonly ModelManager _modelManager;
        private readonly ILogger<WhisperTranscriber> _logger;
        private WhisperFactory _whisperFactory;

        public WhisperTranscriber(IOptions<WhisperServerConfig> config, ModelManager modelManager, ILogger<WhisperTranscriber> logger)
        {
            _config = config.Value;
            _modelManager = modelManager;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            var modelPath = await _modelManager.EnsureModelAsync(_config.ModelName);
            _whisperFactory = WhisperFactory.FromPath(modelPath);
            _logger.LogInformation("Whisper transcriber initialized successfully");
        }

        public async Task<TranscriptionResponse> TranscribeAsync(string audioPath, string language = "auto", float temperature = 0.0f, bool returnTimestamps = false)
        {
            if (_whisperFactory == null)
            {
                await InitializeAsync();
            }

            using var processor = _whisperFactory.CreateBuilder()
                .WithLanguage(language)
                .Build();

            using var fileStream = File.OpenRead(audioPath);
            var transcriptionText = "";
            var segments = new List<TranscriptionSegment>();

            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                transcriptionText += result.Text;
                
                if (returnTimestamps)
                {
                    segments.Add(new TranscriptionSegment
                    {
                        StartTimeMs = (int)(result.Start.TotalMilliseconds),
                        EndTimeMs = (int)(result.End.TotalMilliseconds),
                        Text = result.Text
                    });
                }
            }

            return new TranscriptionResponse
            {
                Text = transcriptionText,
                Segments = returnTimestamps ? segments : null,
                Model = _config.ModelName
            };
        }
    }
}

// Services/TranscriptionService.cs
using Microsoft.Extensions.Options;
using WhisperAPI.Models;
using System.Diagnostics;

namespace WhisperAPI.Services
{
    public class TranscriptionService
    {
        private readonly WhisperTranscriber _transcriber;
        private readonly HistoryLogger _historyLogger;
        private readonly WhisperServerConfig _config;
        private readonly ILogger<TranscriptionService> _logger;

        public TranscriptionService(WhisperTranscriber transcriber, HistoryLogger historyLogger, 
            IOptions<WhisperServerConfig> config, ILogger<TranscriptionService> logger)
        {
            _transcriber = transcriber;
            _historyLogger = historyLogger;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<TranscriptionResponse> TranscribeAsync(TranscriptionRequest request)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            var tempFilePath = Path.Combine(tempDir, request.File.FileName);
            
            try
            {
                // Save uploaded file to temp location
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(fileStream);
                }

                var stopwatch = Stopwatch.StartNew();
                var result = await _transcriber.TranscribeAsync(
                    tempFilePath, 
                    request.Language, 
                    request.Temperature, 
                    request.ReturnTimestamps);
                
                stopwatch.Stop();
                
                result.ProcessingTime = stopwatch.Elapsed.TotalSeconds;
                result.DurationSeconds = GetAudioDuration(tempFilePath);
                
                // Save to history if enabled
                await _historyLogger.SaveAsync(result, request.File.FileName);

                return result;
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir);
            }
        }

        public async Task<TranscriptionResponse> TranscribeFromUrlAsync(string url, string language, float temperature, bool returnTimestamps)
        {
            // Implementation for URL-based transcription
            using var httpClient = new HttpClient();
            var audioData = await httpClient.GetByteArrayAsync(url);
            
            var tempFilePath = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tempFilePath, audioData);
                return await _transcriber.TranscribeAsync(tempFilePath, language, temperature, returnTimestamps);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        public async Task<TranscriptionResponse> TranscribeFromBase64Async(string base64Data, string language, float temperature, bool returnTimestamps)
        {
            // Implementation for Base64-based transcription
            var audioData = Convert.FromBase64String(base64Data);
            var tempFilePath = Path.GetTempFileName();
            
            try
            {
                await File.WriteAllBytesAsync(tempFilePath, audioData);
                return await _transcriber.TranscribeAsync(tempFilePath, language, temperature, returnTimestamps);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        private double GetAudioDuration(string filePath)
        {
            // Placeholder - implement audio duration calculation
            // Could use NAudio or similar library
            return 0.0;
        }
    }
}

// Services/HistoryLogger.cs
using Microsoft.Extensions.Options;
using WhisperAPI.Models;
using System.Text.Json;

namespace WhisperAPI.Services
{
    public class HistoryLogger
    {
        private readonly WhisperServerConfig _config;
        private readonly ILogger<HistoryLogger> _logger;
        private readonly string _historyRoot;

        public HistoryLogger(IOptions<WhisperServerConfig> config, ILogger<HistoryLogger> logger)
        {
            _config = config.Value;
            _logger = logger;
            _historyRoot = Path.Combine(Directory.GetCurrentDirectory(), "history");
            
            if (!Directory.Exists(_historyRoot))
                Directory.CreateDirectory(_historyRoot);
        }

        public async Task SaveAsync(TranscriptionResponse result, string originalFilename)
        {
            try
            {
                var now = DateTime.Now;
                var dateStr = now.ToString("yyyy-MM-dd");
                var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var randomTag = Guid.NewGuid().ToString("N")[..4];
                
                var historyFilename = $"{timestamp}_{Path.GetFileNameWithoutExtension(originalFilename)}_{randomTag}.json";
                var dateDir = Path.Combine(_historyRoot, dateStr);
                
                if (!Directory.Exists(dateDir))
                    Directory.CreateDirectory(dateDir);
                    
                var historyPath = Path.Combine(dateDir, historyFilename);
                
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                await File.WriteAllTextAsync(historyPath, json);
                
                _logger.LogInformation($"Transcription result saved to history: {historyPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving transcription history");
            }
        }
    }
}