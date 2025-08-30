using Serilog;
using WhisperAPI.Configuration;
using WhisperAPI.Filters;
using WhisperAPI.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

var argsList = args.ToList();
string? configPath = null;
for (int i = 0; i < argsList.Count; i++)
{
    if (string.Equals(argsList[i], "--config", StringComparison.OrdinalIgnoreCase) && i + 1 < argsList.Count)
    {
        configPath = argsList[i + 1];
        break;
    }
}

// Handle pre-download command EARLY to avoid building the web host
for (int i = 0; i < argsList.Count - 1; i++)
{
    if (string.Equals(argsList[i], "--download", StringComparison.OrdinalIgnoreCase))
    {
        var model = argsList[i + 1];
        try
        {
            var baseDirEarly = AppContext.BaseDirectory;
            var options = Options.Create(new AppConfiguration());
            using var loggerFactory = LoggerFactory.Create(b => b
                .SetMinimumLevel(LogLevel.Information)
                .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));
            var logger = loggerFactory.CreateLogger<ModelManager>();
            var mm = new ModelManager(options, logger);
            Console.WriteLine($"[download] starting for model='{model}'...");
            var path = await mm.EnsureModelAsync(model, CancellationToken.None);
            var size = new FileInfo(path).Exists ? new FileInfo(path).Length : 0;
            Console.WriteLine($"[download] completed: {path} ({size} bytes)");
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[download] failed: {ex.GetType().Name}: {ex.Message}");
            Environment.ExitCode = 1;
        }
        return; // exit without starting server
    }
}

var builder = WebApplication.CreateBuilder(args);

// Configuration loading: defaults -> file next to exe (optional) -> --config file (optional) -> env (WHISPER_) -> CLI
builder.Configuration
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Server:Host"] = "localhost",
        ["Server:Port"] = "8000",
        ["Server:TimeoutSeconds"] = "10",
        ["Whisper:ModelName"] = "whisper-base",
        ["Whisper:Language"] = "en",
        ["Whisper:Temperature"] = "0.01",
        ["Whisper:ChunkLengthSeconds"] = "25",
        ["Audio:SampleRate"] = "16000",
        ["Audio:MaxFileSizeMb"] = "100",
        ["Audio:AutoResample"] = "false",
        ["Performance:Device"] = "auto",
        ["Performance:MaxConcurrentRequests"] = "4",
        ["Performance:EnableGpu"] = "true",
        ["Logging:Level"] = "Information",
        ["Logging:FilePath"] = "logs/whisper-server.log",
        ["Logging:MaxFileBytes"] = (10 * 1024 * 1024).ToString()
    }!);

// Auto-load config.json next to the executable if found
var baseDir = AppContext.BaseDirectory;
var exeConfigPath = Path.Combine(baseDir, "config.json");
if (File.Exists(exeConfigPath))
{
    builder.Configuration.AddJsonFile(exeConfigPath, optional: true, reloadOnChange: true);
}

if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
{
    builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
}

builder.Configuration.AddEnvironmentVariables(prefix: "WHISPER_");

// Compatibility: accept snake_case keys in config.json by mapping to PascalCase
var compat = new Dictionary<string, string?>();
string? cv(string key) => builder.Configuration[key];
void map(string legacy, string modern)
{
    var v = cv(legacy);
    if (!string.IsNullOrEmpty(v)) compat[modern] = v;
}
// server
map("server:host", "Server:Host");
map("server:port", "Server:Port");
map("server:timeout_seconds", "Server:TimeoutSeconds");
// whisper
map("whisper:model_name", "Whisper:ModelName");
map("whisper:language", "Whisper:Language");
map("whisper:temperature", "Whisper:Temperature");
map("whisper:chunk_length_seconds", "Whisper:ChunkLengthSeconds");
// audio
map("audio:sample_rate", "Audio:SampleRate");
map("audio:max_file_size_mb", "Audio:MaxFileSizeMb");
map("audio:auto_resample", "Audio:AutoResample");
// performance
map("performance:device", "Performance:Device");
map("performance:max_concurrent_requests", "Performance:MaxConcurrentRequests");
map("performance:enable_gpu", "Performance:EnableGpu");
// logging
map("logging:level", "Logging:Level");
map("logging:file_path", "Logging:FilePath");
map("logging:max_file_bytes", "Logging:MaxFileBytes");
if (compat.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(compat);
}

// Map simple CLI flags to config keys for MVP compatibility
var cliOverrides = new Dictionary<string, string?>();
for (int i = 0; i < argsList.Count - 1; i++)
{
    var key = argsList[i];
    var val = argsList[i + 1];
    if (!key.StartsWith("--")) continue;
    switch (key.ToLowerInvariant())
    {
        case "--host":
            cliOverrides["Server:Host"] = val; break;
        case "--port":
            cliOverrides["Server:Port"] = val; break;
        case "--model":
            cliOverrides["Whisper:ModelName"] = val; break;
        case "--language":
            cliOverrides["Whisper:Language"] = val; break;
        case "--timeout":
            cliOverrides["Server:TimeoutSeconds"] = val; break;
    }
}
if (cliOverrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(cliOverrides);
}

// Services
builder.Services.AddRouting();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
builder.Services.Configure<AppConfiguration>(builder.Configuration);
builder.Services.AddSingleton<ModelManager>();
builder.Services.AddSingleton(provider => new ConcurrencyLimiter((provider.GetRequiredService<IOptions<AppConfiguration>>().Value.Performance.MaxConcurrentRequests)));
builder.Services.AddScoped<ConcurrencyLimiterFilter>();
builder.Services.AddHostedService<StartupInitializer>();
builder.Services.AddSingleton<AudioValidationService>();
builder.Services.AddSingleton<WhisperTranscriber>();
builder.Services.AddHttpClient();

// Enforce multipart size limit via form options (still validate in controller)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    var maxMbStr = builder.Configuration["Audio:MaxFileSizeMb"];
    var mb = (maxMbStr is string s && int.TryParse(s, out var parsed)) ? parsed : 100;
    o.MultipartBodyLengthLimit = (long)mb * 1024L * 1024L;
});

// Serilog: console + file with 10MB rotation
var logsDir = Path.Combine(baseDir, "logs");
Directory.CreateDirectory(logsDir);
var cfg = builder.Configuration.Get<AppConfiguration>() ?? new AppConfiguration();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(Enum.TryParse<Serilog.Events.LogEventLevel>(cfg.Logging.Level, true, out var lvl) ? lvl : Serilog.Events.LogEventLevel.Information)
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.IsPathRooted(cfg.Logging.FilePath) ? cfg.Logging.FilePath : Path.Combine(baseDir, cfg.Logging.FilePath),
        rollingInterval: RollingInterval.Infinite,
        fileSizeLimitBytes: cfg.Logging.MaxFileBytes,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 10,
        shared: true)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

app.UseCors("AllowAll");
app.MapControllers();
app.MapGet("/health", () => Results.Json(new { status = "ok", version = "0.1.0" }));

// Pre-download command: --download <model>
string? downloadModel = null;
for (int i = 0; i < argsList.Count - 1; i++)
{
    if (string.Equals(argsList[i], "--download", StringComparison.OrdinalIgnoreCase))
    {
        downloadModel = argsList[i + 1];
        break;
    }
}

// (download handled above before host build)

app.Logger.LogInformation("Whisper Server starting on {Host}:{Port} using model {Model}", cfg.Server.Host, cfg.Server.Port, cfg.Whisper.ModelName);

app.Run($"http://{cfg.Server.Host}:{cfg.Server.Port}");

public partial class Program { }
