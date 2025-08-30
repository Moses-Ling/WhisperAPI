# Windows Whisper API Server - Software Development Plan

**Project**: Windows Whisper API Server MVP  
**Version**: 1.0  
**Duration**: 6-8 weeks  
**Team Size**: 1-2 developers  
**Framework**: .NET 8.0 / Visual Studio 2022  

## 1. Development Overview

### 1.1 Development Methodology
- **Iterative Development**: Build, test, and validate each phase completely
- **Continuous Integration**: Automated build and test after every commit
- **Test-Driven Approach**: Unit tests written alongside implementation
- **Integration Testing**: End-to-end testing with ClioAI client after each phase

### 1.2 Quality Gates
Each phase must pass all quality gates before proceeding:
- ✅ **Build Success**: Clean compilation with zero warnings
- ✅ **Unit Tests**: >80% code coverage, all tests passing
- ✅ **Integration Tests**: API compatibility verified
- ✅ **Performance Tests**: Meet or exceed target RTF ratios
- ✅ **ClioAI Integration**: Successful end-to-end transcription

---

## 2. Phase 1: Foundation & Core API (Weeks 1-3)

### 2.1 Phase 1 Objectives
- Establish project foundation and architecture
- Implement core OpenAI-compatible API endpoints
- Integrate Whisper.net with basic model loading
- Enable automatic model downloading
- Achieve basic transcription functionality

### 2.2 Week 1: Project Setup & Infrastructure

#### 2.2.1 Day 1-2: Environment & Project Setup
**Tasks:**
- [ ] Create Visual Studio 2022 solution structure
- [ ] Setup .NET 8.0 project with required NuGet packages
- [ ] Configure project properties and dependencies
- [ ] Setup Git repository with proper .gitignore
- [ ] Create initial project structure (Controllers, Services, Models, Configuration)

**NuGet Packages:**
```xml
<PackageReference Include="Microsoft.AspNetCore.App" />
<PackageReference Include="Whisper.net" Version="1.8.1" />
<PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.8.1" />
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="NAudio" Version="2.2.1" />
<PackageReference Include="xUnit" Version="2.4.2" />
<PackageReference Include="xUnit.runner.visualstudio" Version="2.4.5" />
<PackageReference Include="Moq" Version="4.20.69" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

**Deliverables:**
- [ ] Complete project structure
- [ ] Build pipeline configuration
- [ ] Initial README and documentation

**Testing & Validation:**
```bash
# Build validation
dotnet clean
dotnet restore
dotnet build --configuration Release
# Should complete with 0 warnings

# Project structure validation
whisper-server.exe --help
# Should display help text
```

#### 2.2.2 Day 3-4: Basic Configuration & CLI Framework
**Tasks:**
- [ ] Implement `ServerConfiguration` classes
- [ ] Create `System.CommandLine` CLI argument parsing
- [ ] Setup Serilog logging with file and console outputs
- [ ] Implement basic `Program.cs` with CLI handling
- [ ] Create configuration loading from JSON/Environment variables

**Key Files:**
```csharp
// Configuration/ServerConfiguration.cs
public class ServerConfiguration
{
    public ServerSettings Server { get; set; } = new();
    public WhisperConfiguration Whisper { get; set; } = new();
    public AudioConfiguration Audio { get; set; } = new();
    // ... other configurations
}

// Program.cs - CLI Framework
public static async Task<int> Main(string[] args)
{
    var rootCommand = new RootCommand("Whisper Server");
    // Add options: --host, --port, --model, --language, --config
    return await rootCommand.InvokeAsync(args);
}
```

**Testing & Validation:**
```bash
# CLI validation
whisper-server.exe --help
whisper-server.exe --host localhost --port 8000 --model base

# Configuration validation
echo '{"server":{"port":9000}}' > test-config.json
whisper-server.exe --config test-config.json
# Should load configuration correctly

# Unit Tests
dotnet test --logger "console;verbosity=detailed"
# All configuration tests should pass
```

#### 2.2.3 Day 5: ASP.NET Core Web API Setup
**Tasks:**
- [ ] Configure ASP.NET Core with Kestrel server
- [ ] Setup CORS middleware for ClioAI compatibility
- [ ] Create base controller structure
- [ ] Implement health check endpoint
- [ ] Add Swagger/OpenAPI documentation

**Key Implementation:**
```csharp
// Controllers/HealthController.cs
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult GetHealth() => Ok(new { status = "ok", version = "1.0.0" });
}

// Program.cs - Web API Setup
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddCors(options => {
    options.AddPolicy("AllowClioAI", policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("AllowClioAI");
app.MapControllers();
```

**Testing & Validation:**
```bash
# Start server
whisper-server.exe --port 8000

# Health check test
curl http://localhost:8000/health
# Expected: {"status":"ok","version":"1.0.0"}

# CORS test
curl -H "Origin: http://localhost:3000" -H "Access-Control-Request-Method: POST" \
     -X OPTIONS http://localhost:8000/v1/audio/transcriptions
# Should return CORS headers
```

### 2.3 Week 2: Model Management & Whisper Integration

#### 2.3.1 Day 6-7: Model Manager Implementation
**Tasks:**
- [ ] Implement `ModelManager` class with auto-download
- [ ] Add progress reporting for model downloads
- [ ] Implement model validation and integrity checks
- [ ] Create model-to-GgmlType mapping
- [ ] Add retry logic for failed downloads

**Key Implementation:**
```csharp
// Services/ModelManager.cs
public class ModelManager
{
    public async Task<string> EnsureModelAsync(string modelName)
    {
        var modelPath = GetModelPath(modelName);
        if (File.Exists(modelPath)) return modelPath;

        // Download with progress reporting
        var ggmlType = MapModelNameToGgmlType(modelName);
        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
        
        // Save with progress tracking
        await SaveModelWithProgress(modelStream, modelPath);
        return modelPath;
    }
}
```

**Testing & Validation:**
```bash
# Model download test
whisper-server.exe download tiny
whisper-server.exe download base
# Should download and report progress

# Model validation test
whisper-server.exe list-models
# Should show available models

# Unit tests for ModelManager
dotnet test --filter "Category=ModelManager"
# All model management tests should pass
```

#### 2.3.2 Day 8-9: Whisper Integration & Basic Transcription
**Tasks:**
- [ ] Implement `WhisperTranscriber` class
- [ ] Integrate Whisper.net with CUDA support detection
- [ ] Create basic transcription service
- [ ] Implement device selection logic (CUDA > CPU)
- [ ] Add basic error handling and logging

**Key Implementation:**
```csharp
// Services/WhisperTranscriber.cs
public class WhisperTranscriber
{
    private WhisperFactory _whisperFactory;

    public async Task InitializeAsync(string modelPath)
    {
        _whisperFactory = WhisperFactory.FromPath(modelPath);
        // Test CUDA availability
        var deviceInfo = DetectOptimalDevice();
        _logger.LogInformation($"Using device: {deviceInfo}");
    }

    public async Task<TranscriptionResult> TranscribeAsync(string audioPath)
    {
        using var processor = _whisperFactory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        // Process audio file
        using var fileStream = File.OpenRead(audioPath);
        var result = new StringBuilder();
        
        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            result.Append(segment.Text);
        }

        return new TranscriptionResult { Text = result.ToString() };
    }
}
```

**Testing & Validation:**
```bash
# Basic transcription test
echo "Hello world" | text-to-speech > test.wav
whisper-server.exe test test.wav
# Should return transcribed text

# CUDA detection test
whisper-server.exe --log-level Debug test test.wav
# Logs should show CUDA detection and device selection

# Integration test with temporary file
dotnet test --filter "Category=Transcription"
# Transcription tests should pass with sample audio files
```

#### 2.3.3 Day 10: Models API Endpoints
**Tasks:**
- [ ] Implement `ModelsController` with OpenAI compatibility
- [ ] Add model listing and details endpoints
- [ ] Create model status and availability checking
- [ ] Add model metadata (size, description, etc.)

**Key Implementation:**
```csharp
// Controllers/ModelsController.cs
[ApiController]
[Route("v1/[controller]")]
public class ModelsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetModels()
    {
        var availableModels = await _modelManager.GetAvailableModelsAsync();
        return Ok(new ModelsResponse
        {
            Object = "list",
            Data = availableModels.Select(m => new ModelInfo
            {
                Id = m.Name,
                Object = "model",
                OwnedBy = "openai"
            }).ToList()
        });
    }

    [HttpGet("{modelId}")]
    public async Task<IActionResult> GetModel(string modelId)
    {
        var model = await _modelManager.GetModelAsync(modelId);
        if (model == null) return NotFound();
        
        return Ok(new ModelInfo
        {
            Id = modelId,
            Object = "model",
            OwnedBy = "openai"
        });
    }
}
```

**Testing & Validation:**
```bash
# Start server with models
whisper-server.exe --model base

# Test models API
curl http://localhost:8000/v1/models
# Should return OpenAI-compatible models list

curl http://localhost:8000/v1/models/base
# Should return model details

curl http://localhost:8000/v1/models/nonexistent
# Should return 404 Not Found

# API compatibility test
dotnet test --filter "Category=ModelsAPI"
# Models API tests should pass
```

### 2.4 Week 3: Audio Transcription API

#### 2.4.1 Day 11-12: Audio Processing & Validation
**Tasks:**
- [ ] Implement `AudioValidationService` 
- [ ] Add support for multiple audio formats (WAV, MP3, M4A, FLAC, OGG)
- [ ] Create audio format detection and validation
- [ ] Implement optimal format detection (16kHz, mono, PCM)
- [ ] Add audio duration calculation

**Key Implementation:**
```csharp
// Services/AudioValidationService.cs
public class AudioValidationService
{
    public AudioValidationResult ValidateAudioFile(string filePath)
    {
        using var reader = new AudioFileReader(filePath);
        var format = reader.WaveFormat;
        
        var result = new AudioValidationResult
        {
            IsValid = true,
            OriginalSampleRate = format.SampleRate,
            OriginalChannels = format.Channels,
            DurationSeconds = (float)reader.TotalTime.TotalSeconds,
            IsOptimalFormat = IsOptimalFormat(format)
        };

        if (result.IsOptimalFormat)
        {
            _logger.LogInformation("✅ OPTIMAL: Audio format matches ClioAI output");
        }
        else
        {
            _logger.LogWarning("⚠️ SUBOPTIMAL: Audio requires processing");
        }

        return result;
    }

    private bool IsOptimalFormat(WaveFormat format)
    {
        return format.SampleRate == 16000 && 
               format.Channels == 1 && 
               format.BitsPerSample == 16;
    }
}
```

**Testing & Validation:**
```bash
# Create test audio files in different formats
# 16kHz mono (optimal)
ffmpeg -f lavfi -i "sine=frequency=1000:duration=5" -ar 16000 -ac 1 test-optimal.wav

# 48kHz stereo (suboptimal)
ffmpeg -f lavfi -i "sine=frequency=1000:duration=5" -ar 48000 -ac 2 test-suboptimal.wav

# Test validation
whisper-server.exe test test-optimal.wav --log-level Debug
# Should log "OPTIMAL" format

whisper-server.exe test test-suboptimal.wav --log-level Debug
# Should log "SUBOPTIMAL" format

# Unit tests
dotnet test --filter "Category=AudioValidation"
# Audio validation tests should pass
```

#### 2.4.2 Day 13-14: Transcription Service Implementation
**Tasks:**
- [ ] Implement `TranscriptionService` with complete request handling
- [ ] Add multipart form data processing
- [ ] Implement temporary file management with cleanup
- [ ] Add request/response logging
- [ ] Create performance monitoring (RTF tracking)

**Key Implementation:**
```csharp
// Services/TranscriptionService.cs
public class TranscriptionService
{
    public async Task<TranscriptionResponse> TranscribeAsync(IFormFile audioFile, TranscriptionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var tempFilePath = await SaveTemporaryFile(audioFile);
        
        try
        {
            // Validate audio format
            var validation = _audioValidationService.ValidateAudioFile(tempFilePath);
            
            // Log performance optimization
            if (validation.IsOptimalFormat)
            {
                _logger.LogInformation("🚀 FAST PATH: Using optimal audio format");
            }
            
            // Transcribe
            var result = await _whisperTranscriber.TranscribeAsync(tempFilePath);
            
            stopwatch.Stop();
            var response = new TranscriptionResponse
            {
                Text = result.Text,
                ProcessingTime = stopwatch.Elapsed.TotalSeconds,
                DurationSeconds = validation.DurationSeconds,
                Model = request.Model ?? "base"
            };

            // Log performance metrics
            var rtf = response.ProcessingTime / response.DurationSeconds;
            _logger.LogInformation($"RTF: {rtf:F2}x ({response.ProcessingTime:F1}s for {response.DurationSeconds:F1}s audio)");
            
            return response;
        }
        finally
        {
            // Cleanup temporary file
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }
}
```

**Testing & Validation:**
```bash
# Performance test with different file sizes
whisper-server.exe test test-5sec.wav --log-level Information
whisper-server.exe test test-25sec.wav --log-level Information
# Should log RTF ratios and performance metrics

# Memory usage test
# Start server and monitor memory usage during transcription
whisper-server.exe --model base
# Monitor with Task Manager or performance counters

# Unit tests
dotnet test --filter "Category=TranscriptionService"
# Transcription service tests should pass
```

#### 2.4.3 Day 15: Audio API Controller Implementation
**Tasks:**
- [ ] Implement `AudioController` with OpenAI-compatible endpoints
- [ ] Add `/v1/audio/transcriptions` POST endpoint
- [ ] Implement error handling with proper HTTP status codes
- [ ] Add request validation and file size limits
- [ ] Create OpenAI-compatible response formatting

**Key Implementation:**
```csharp
// Controllers/AudioController.cs
[ApiController]
[Route("v1/audio")]
public class AudioController : ControllerBase
{
    [HttpPost("transcriptions")]
    public async Task<IActionResult> Transcribe([FromForm] TranscriptionRequest request)
    {
        // Validate request
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new ErrorResponse 
            { 
                Error = new ErrorDetail
                {
                    Message = "No audio file provided",
                    Type = "invalid_request_error",
                    Code = "missing_file"
                }
            });
        }

        // Check file size
        if (request.File.Length > _config.MaxFileSizeBytes)
        {
            return StatusCode(413, new ErrorResponse 
            { 
                Error = new ErrorDetail
                {
                    Message = $"File too large. Maximum size is {_config.MaxFileSizeMB}MB",
                    Type = "invalid_request_error", 
                    Code = "file_too_large"
                }
            });
        }

        try
        {
            var result = await _transcriptionService.TranscribeAsync(request.File, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return StatusCode(500, new ErrorResponse 
            { 
                Error = new ErrorDetail
                {
                    Message = "Internal server error during transcription",
                    Type = "server_error",
                    Code = "transcription_failed"
                }
            });
        }
    }
}
```

**Testing & Validation:**
```bash
# Start server
whisper-server.exe --host localhost --port 8000 --model base

# Test transcription API with curl
curl -X POST http://localhost:8000/v1/audio/transcriptions \
  -F "file=@test-audio.wav" \
  -F "model=base"
# Should return OpenAI-compatible JSON response

# Test error cases
curl -X POST http://localhost:8000/v1/audio/transcriptions
# Should return 400 Bad Request

# Create large file for size limit test
dd if=/dev/zero of=large-file.wav bs=1M count=101
curl -X POST http://localhost:8000/v1/audio/transcriptions \
  -F "file=@large-file.wav"
# Should return 413 Payload Too Large

# API compatibility test
dotnet test --filter "Category=AudioAPI"
# Audio API tests should pass
```

### 2.5 Phase 1 Integration Testing & Quality Gate

#### 2.5.1 Day 16-17: Comprehensive Testing
**Tasks:**
- [ ] Run full unit test suite with coverage analysis
- [ ] Perform integration testing with real audio files
- [ ] Conduct performance benchmarking
- [ ] Test ClioAI integration end-to-end
- [ ] Validate OpenAI API compatibility

**Test Suite Execution:**
```bash
# Full unit test suite with coverage
dotnet test --collect:"XPlat Code Coverage" --settings:coverlet.runsettings
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
# Target: >80% code coverage

# Integration tests
dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"

# Performance benchmark
whisper-server.exe --model base --log-level Information
# Test with 5s, 10s, 25s audio files
# Verify RTF < 1.0x for base model

# ClioAI Integration Test
# 1. Start whisper server: whisper-server.exe --port 5042 --model base
# 2. Configure ClioAI to use local server: http://localhost:5042
# 3. Record and transcribe audio in ClioAI
# 4. Verify successful transcription
```

**Quality Gate Checklist:**
- [ ] ✅ Build: Clean compilation, zero warnings
- [ ] ✅ Unit Tests: >80% coverage, all tests pass
- [ ] ✅ Integration: All API endpoints functional
- [ ] ✅ Performance: Base model RTF < 1.0x on recommended hardware
- [ ] ✅ ClioAI: Successful end-to-end transcription
- [ ] ✅ Memory: No memory leaks during 1-hour operation
- [ ] ✅ Logging: Comprehensive structured logging

#### 2.5.2 Day 18: Phase 1 Deployment Package
**Tasks:**
- [ ] Create release build configuration
- [ ] Package application with all dependencies
- [ ] Create installation guide
- [ ] Prepare Phase 1 demonstration
- [ ] Document known issues and limitations

**Deployment Package:**
```bash
# Create release build
dotnet publish -c Release -r win-x64 --self-contained true

# Package structure
whisper-server-v1.0-phase1/
├── whisper-server.exe
├── config.json
├── appsettings.json
├── README.md
├── INSTALL.md
└── samples/
    ├── test-audio.wav
    └── benchmark-suite/
```

**Phase 1 Deliverables:**
- [ ] ✅ Functional Whisper API server
- [ ] ✅ OpenAI-compatible endpoints
- [ ] ✅ Automatic model downloading
- [ ] ✅ ClioAI integration verified
- [ ] ✅ Basic CLI interface
- [ ] ✅ Performance monitoring
- [ ] ✅ Comprehensive test suite

---

## 3. Phase 2: Service & Optimization (Weeks 4-5)

### 3.1 Phase 2 Objectives
- Implement Windows service functionality
- Add advanced audio optimization features
- Enhance performance monitoring and logging
- Implement configuration file management
- Add robust error handling and recovery

### 3.2 Week 4: Windows Service Implementation

#### 3.2.1 Day 19-20: Windows Service Infrastructure
**Tasks:**
- [ ] Add Microsoft.Extensions.Hosting.WindowsServices package
- [ ] Implement service lifecycle management
- [ ] Create service installation/uninstallation logic
- [ ] Add service configuration and registry management
- [ ] Implement service logging to Windows Event Log

**Key Implementation:**
```csharp
// Services/WhisperWindowsService.cs
public class WhisperWindowsService : BackgroundService
{
    private readonly IHostApplicationLifetime _hostLifetime;
    private WebApplication _webApp;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Configure and start web application
            var builder = WebApplication.CreateBuilder();
            ConfigureServices(builder);
            
            _webApp = builder.Build();
            ConfigurePipeline(_webApp);
            
            await _webApp.StartAsync(stoppingToken);
            
            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Service failed to start");
            _hostLifetime.StopApplication();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_webApp != null)
        {
            await _webApp.StopAsync(cancellationToken);
            await _webApp.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}

// Program.cs - Service Mode
public static async Task RunAsWindowsService(ServerConfiguration config)
{
    var host = Host.CreateDefaultBuilder()
        .UseWindowsService(options =>
        {
            options.ServiceName = "WhisperAPIService";
        })
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton(config);
            services.AddHostedService<WhisperWindowsService>();
        })
        .UseSerilog((context, logger) =>
        {
            logger.WriteTo.EventLog("Whisper API Server", manageEventSource: true);
        })
        .Build();

    await host.RunAsync();
}
```

**Testing & Validation:**
```bash
# Install service
whisper-server.exe --install-service --config production.json
# Should register Windows service

# Verify service installation
sc query WhisperAPIService
# Should show service status

# Start service
net start WhisperAPIService
# Should start successfully

# Test service functionality
curl http://localhost:8000/health
# Should respond while running as service

# Stop and uninstall service
net stop WhisperAPIService
whisper-server.exe --uninstall-service

# Unit tests
dotnet test --filter "Category=WindowsService"
# Service management tests should pass
```

#### 3.2.2 Day 21-22: Service Management & Configuration
**Tasks:**
- [ ] Implement service configuration management
- [ ] Add configuration file hot-reload support
- [ ] Create service status monitoring
- [ ] Implement automatic service recovery
- [ ] Add Windows Event Log integration

**Key Implementation:**
```csharp
// Services/ConfigurationWatcher.cs
public class ConfigurationWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly IOptionsMonitor<ServerConfiguration> _options;

    public ConfigurationWatcher(string configPath, IOptionsMonitor<ServerConfiguration> options)
    {
        _options = options;
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(configPath))
        {
            Filter = Path.GetFileName(configPath),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Changed += OnConfigChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private async void OnConfigChanged(object sender, FileSystemEventArgs e)
    {
        await Task.Delay(500); // Debounce file changes
        
        try
        {
            _logger.LogInformation("Configuration file changed, reloading...");
            // Trigger configuration reload
            _options.CurrentValue.ReloadFromFile(e.FullPath);
            _logger.LogInformation("Configuration reloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration");
        }
    }
}

// Services/ServiceHealthMonitor.cs
public class ServiceHealthMonitor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check service health
                var health = await CheckServiceHealth();
                
                if (!health.IsHealthy)
                {
                    _logger.LogWarning($"Service health check failed: {health.Message}");
                    // Implement recovery logic
                    await AttemptServiceRecovery();
                }
                
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health monitor error");
            }
        }
    }
}
```

**Testing & Validation:**
```bash
# Test configuration hot-reload
whisper-server.exe --service --config test-config.json &
echo '{"server":{"port":9000}}' > test-config.json
# Should reload configuration and log message

# Test service recovery
# Kill service process and verify automatic restart
taskkill /F /IM whisper-server.exe
# Service should restart automatically

# Test Windows Event Log integration
# Check Event Viewer for Whisper API Server events
eventvwr.exe
# Should show service start/stop/error events

# Integration tests
dotnet test --filter "Category=ServiceManagement"
# Service management tests should pass
```

#### 3.2.3 Day 23: Advanced CLI Commands
**Tasks:**
- [ ] Implement `list-models` command with detailed info
- [ ] Add `download` command with progress reporting
- [ ] Create `test` command for audio file testing
- [ ] Add `benchmark` command for performance testing
- [ ] Implement `status` command for service monitoring

**Key Implementation:**
```csharp
// Commands/ListModelsCommand.cs
public static Command CreateListModelsCommand()
{
    var command = new Command("list-models", "List available Whisper models");
    
    command.SetHandler(async () =>
    {
        Console.WriteLine("Available Whisper models:");
        Console.WriteLine();
        
        var models = new[]
        {
            new { Name = "tiny", Size = "39 MB", Speed = "Fastest", Accuracy = "Lowest" },
            new { Name = "base", Size = "142 MB", Speed = "Fast", Accuracy = "Good" },
            new { Name = "small", Size = "244 MB", Speed = "Medium", Accuracy = "Better" },
            new { Name = "medium", Size = "769 MB", Speed = "Slow", Accuracy = "High" },
            new { Name = "large-v3", Size = "1550 MB", Speed = "Slowest", Accuracy = "Highest" }
        };

        foreach (var model in models)
        {
            var status = await CheckModelAvailability(model.Name);
            Console.WriteLine($"  {model.Name,-12} {model.Size,-8} {model.Speed,-8} {model.Accuracy,-8} {status}");
        }
    });
    
    return command;
}

// Commands/TestCommand.cs
public static Command CreateTestCommand()
{
    var audioFileArg = new Argument<string>("audio-file", "Path to audio file");
    var modelOption = new Option<string>("--model", () => "base", "Model to use for testing");
    
    var command = new Command("test", "Test transcription with audio file")
    {
        audioFileArg,
        modelOption
    };
    
    command.SetHandler(async (audioFile, model) =>
    {
        if (!File.Exists(audioFile))
        {
            Console.WriteLine($"❌ Audio file not found: {audioFile}");
            return;
        }

        Console.WriteLine($"🎵 Testing transcription: {Path.GetFileName(audioFile)}");
        Console.WriteLine($"📊 Model: {model}");
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Initialize transcriber
            var transcriber = new WhisperTranscriber();
            await transcriber.InitializeAsync(model);
            
            // Transcribe file
            var result = await transcriber.TranscribeAsync(audioFile);
            stopwatch.Stop();
            
            Console.WriteLine();
            Console.WriteLine($"✅ Transcription: {result.Text}");
            Console.WriteLine($"⏱️ Time: {stopwatch.Elapsed.TotalSeconds:F2}s");
            
            // Calculate RTF
            var audioDuration = GetAudioDuration(audioFile);
            var rtf = stopwatch.Elapsed.TotalSeconds / audioDuration;
            Console.WriteLine($"🚀 RTF: {rtf:F2}x");
            
            if (rtf < 1.0)
                Console.WriteLine($"✅ Real-time performance achieved!");
            else
                Console.WriteLine($"⚠️ Slower than real-time");
        }
        catch (