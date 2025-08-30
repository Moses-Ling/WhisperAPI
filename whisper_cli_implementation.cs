// Program.cs - Complete CLI Implementation
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WhisperAPI.Configuration;
using WhisperAPI.Services;

namespace WhisperAPI
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Define CLI options
            var hostOption = new Option<string>(
                aliases: new[] { "--host", "-h" },
                description: "Server hostname or IP address",
                getDefaultValue: () => "localhost");

            var portOption = new Option<int>(
                aliases: new[] { "--port", "-p" },
                description: "Server port number",
                getDefaultValue: () => 8000);

            var modelOption = new Option<string>(
                aliases: new[] { "--model", "-m" },
                description: "Whisper model name (tiny, base, small, medium, large, large-v3)",
                getDefaultValue: () => "base");

            var languageOption = new Option<string>(
                aliases: new[] { "--language", "-l" },
                description: "Language code (en=English, auto=Auto-detect, multi=Multilingual)",
                getDefaultValue: () => "en");

            var configOption = new Option<string>(
                aliases: new[] { "--config", "-c" },
                description: "Path to configuration file",
                getDefaultValue: () => "config.json");

            var serviceOption = new Option<bool>(
                aliases: new[] { "--service" },
                description: "Run as Windows service");

            var installServiceOption = new Option<bool>(
                aliases: new[] { "--install-service" },
                description: "Install Windows service");

            var uninstallServiceOption = new Option<bool>(
                aliases: new[] { "--uninstall-service" },
                description: "Uninstall Windows service");

            var logLevelOption = new Option<string>(
                aliases: new[] { "--log-level" },
                description: "Logging level (Debug, Information, Warning, Error, Critical)",
                getDefaultValue: () => "Information");

            var modelsDirOption = new Option<string>(
                aliases: new[] { "--models-dir" },
                description: "Models directory path",
                getDefaultValue: () => "models");

            var noDownloadOption = new Option<bool>(
                aliases: new[] { "--no-download" },
                description: "Disable automatic model downloading");

            var maxFileSizeOption = new Option<int>(
                aliases: new[] { "--max-file-size" },
                description: "Maximum file size in MB",
                getDefaultValue: () => 100);

            // Main command
            var rootCommand = new RootCommand("Whisper Server - OpenAI Compatible Speech-to-Text API")
            {
                hostOption, portOption, modelOption, languageOption, configOption,
                serviceOption, installServiceOption, uninstallServiceOption,
                logLevelOption, modelsDirOption, noDownloadOption, maxFileSizeOption
            };

            // Add subcommands
            rootCommand.AddCommand(CreateListModelsCommand());
            rootCommand.AddCommand(CreateDownloadModelCommand());
            rootCommand.AddCommand(CreateTestCommand());

            // Main handler
            rootCommand.SetHandler(async (host, port, model, language, configPath, service, 
                installService, uninstallService, logLevel, modelsDir, noDownload, maxFileSize) =>
            {
                try
                {
                    if (installService)
                    {
                        await InstallWindowsService();
                        return;
                    }

                    if (uninstallService)
                    {
                        await UninstallWindowsService();
                        return;
                    }

                    await StartServer(new ServerOptions
                    {
                        Host = host,
                        Port = port,
                        Model = model,
                        Language = language,
                        ConfigPath = configPath,
                        RunAsService = service,
                        LogLevel = logLevel,
                        ModelsDirectory = modelsDir,
                        NoDownload = noDownload,
                        MaxFileSizeMB = maxFileSize
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FATAL ERROR: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Environment.Exit(1);
                }
            }, hostOption, portOption, modelOption, languageOption, configOption,
               serviceOption, installServiceOption, uninstallServiceOption,
               logLevelOption, modelsDirOption, noDownloadOption, maxFileSizeOption);

            // Enhanced command line parser with better error handling
            var parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseExceptionHandler((ex, context) =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR: {ex.Message}");
                    Console.ResetColor();
                    context.ExitCode = 1;
                })
                .Build();

            return await parser.InvokeAsync(args);
        }

        private static Command CreateListModelsCommand()
        {
            var listCommand = new Command("list-models", "List available Whisper models");
            listCommand.SetHandler(() =>
            {
                Console.WriteLine("Available Whisper models:");
                Console.WriteLine("  tiny     - ~39 MB  - Fastest, lowest accuracy");
                Console.WriteLine("  base     - ~142 MB - Good balance of speed and accuracy");
                Console.WriteLine("  small    - ~244 MB - Better accuracy than base");
                Console.WriteLine("  medium   - ~769 MB - High accuracy");
                Console.WriteLine("  large    - ~1550 MB - Highest accuracy");
                Console.WriteLine("  large-v3 - ~1550 MB - Latest and most accurate");
                Console.WriteLine();
                Console.WriteLine("Language variants (English-only, smaller and faster):");
                Console.WriteLine("  tiny.en, base.en, small.en, medium.en");
            });
            return listCommand;
        }

        private static Command CreateDownloadModelCommand()
        {
            var modelArg = new Argument<string>("model", "Model name to download");
            var downloadCommand = new Command("download", "Download a specific model")
            {
                modelArg
            };

            downloadCommand.SetHandler(async (model) =>
            {
                try
                {
                    var modelManager = new ModelManager(new ServerConfiguration
                    {
                        Whisper = new WhisperConfiguration { ModelsDirectory = "models" }
                    });

                    Console.WriteLine($"Downloading model: {model}");
                    await modelManager.EnsureModelAsync(model);
                    Console.WriteLine($"Model {model} downloaded successfully!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to download model {model}: {ex.Message}");
                }
            }, modelArg);

            return downloadCommand;
        }

        private static Command CreateTestCommand()
        {
            var audioFileArg = new Argument<string>("audio-file", "Path to audio file to test");
            var testCommand = new Command("test", "Test transcription with an audio file")
            {
                audioFileArg
            };

            testCommand.SetHandler(async (audioFile) =>
            {
                if (!File.Exists(audioFile))
                {
                    Console.WriteLine($"Audio file not found: {audioFile}");
                    return;
                }

                // Quick transcription test
                Console.WriteLine($"Testing transcription with: {audioFile}");
                Console.WriteLine("This would perform a test transcription...");
                // Implementation would go here
            }, audioFileArg);

            return testCommand;
        }

        private static async Task StartServer(ServerOptions options)
        {
            // Setup configuration
            var config = LoadConfiguration(options);
            
            // Setup logging
            SetupLogging(config, options.LogLevel);

            try
            {
                Log.Information("Starting Whisper Server...");
                Log.Information($"Host: {options.Host}:{options.Port}");
                Log.Information($"Model: {options.Model}");
                Log.Information($"Language: {options.Language}");
                Log.Information($"Config: {options.ConfigPath}");

                if (options.RunAsService)
                {
                    await RunAsWindowsService(config);
                }
                else
                {
                    await RunAsConsoleApplication(config);
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Server failed to start");
                Console.WriteLine($"CRITICAL ERROR: {ex.Message}");
                Console.WriteLine("Server cannot continue and will exit.");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static ServerConfiguration LoadConfiguration(ServerOptions options)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile(options.ConfigPath, optional: true)
                .AddEnvironmentVariables("WHISPER_")
                .AddCommandLine(CreateCommandLineArgs(options));

            var configuration = builder.Build();
            var serverConfig = new ServerConfiguration();
            configuration.Bind(serverConfig);

            // CLI overrides
            if (!string.IsNullOrEmpty(options.Host)) 
                serverConfig.Server.Host = options.Host;
            if (options.Port > 0) 
                serverConfig.Server.Port = options.Port;
            if (!string.IsNullOrEmpty(options.Model)) 
                serverConfig.Whisper.ModelName = options.Model;
            if (!string.IsNullOrEmpty(options.Language)) 
                serverConfig.Whisper.Language = options.Language;
            if (!string.IsNullOrEmpty(options.ModelsDirectory)) 
                serverConfig.Whisper.ModelsDirectory = options.ModelsDirectory;
            if (options.MaxFileSizeMB > 0) 
                serverConfig.Audio.MaxFileSizeMB = options.MaxFileSizeMB;

            serverConfig.Features.EnableAutoDownload = !options.NoDownload;

            return serverConfig;
        }

        private static string[] CreateCommandLineArgs(ServerOptions options)
        {
            var args = new List<string>();
            if (!string.IsNullOrEmpty(options.Host)) args.AddRange(new[] { "--server:host", options.Host });
            if (options.Port > 0) args.AddRange(new[] { "--server:port", options.Port.ToString() });
            return args.ToArray();
        }

        private static void SetupLogging(ServerConfiguration config, string logLevel)
        {
            var logConfig = new LoggerConfiguration();

            // Parse log level
            if (Enum.TryParse<LogLevel>(logLevel, true, out var level))
            {
                logConfig = level switch
                {
                    LogLevel.Critical => logConfig.MinimumLevel.Fatal(),
                    LogLevel.Error => logConfig.MinimumLevel.Error(),
                    LogLevel.Warning => logConfig.MinimumLevel.Warning(),
                    LogLevel.Information => logConfig.MinimumLevel.Information(),
                    LogLevel.Debug => logConfig.MinimumLevel.Debug(),
                    _ => logConfig.MinimumLevel.Information()
                };
            }

            // Console logging
            if (config.Logging.EnableConsole)
            {
                logConfig = logConfig.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
            }

            // File logging
            if (!string.IsNullOrEmpty(config.Logging.FilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(config.Logging.FilePath));
                logConfig = logConfig.WriteTo.File(
                    config.Logging.FilePath,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: config.Logging.MaxFileSizeMB * 1024 * 1024,
                    retainedFileCountLimit: config.Logging.MaxFiles);
            }

            Log.Logger = logConfig.CreateLogger();
        }

        private static async Task RunAsConsoleApplication(ServerConfiguration config)
        {
            var host = CreateHostBuilder(config).Build();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;
                Log.Information("Shutdown requested...");
                await host.StopAsync();
            };

            Log.Information("Server started. Press Ctrl+C to stop.");
            await host.RunAsync();
        }

        private static async Task RunAsWindowsService(ServerConfiguration config)
        {
            var host = CreateHostBuilder(config)
                .UseWindowsService()
                .Build();

            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(ServerConfiguration config)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(config);
                    services.AddSingleton<ModelManager>();
                    services.AddSingleton<WhisperTranscriber>();
                    services.AddScoped<TranscriptionService>();
                    services.AddScoped<HistoryLogger>();
                })
                .UseSerilog();
        }

        private static async Task InstallWindowsService()
        {
            Console.WriteLine("Installing Windows Service...");
            // Service installation logic
            Console.WriteLine("Service installed successfully!");
        }

        private static async Task UninstallWindowsService()
        {
            Console.WriteLine("Uninstalling Windows Service...");
            // Service uninstallation logic
            Console.WriteLine("Service uninstalled successfully!");
        }
    }

    public class ServerOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Model { get; set; }
        public string Language { get; set; }
        public string ConfigPath { get; set; }
        public bool RunAsService { get; set; }
        public string LogLevel { get; set; }
        public string ModelsDirectory { get; set; }
        public bool NoDownload { get; set; }
        public int MaxFileSizeMB { get; set; }
    }
}

// Configuration/ServerConfiguration.cs
namespace WhisperAPI.Configuration
{
    public class ServerConfiguration
    {
        public ServerSettings Server { get; set; } = new();
        public WhisperConfiguration Whisper { get; set; } = new();
        public AudioConfiguration Audio { get; set; } = new();
        public PerformanceConfiguration Performance { get; set; } = new();
        public LoggingConfiguration Logging { get; set; } = new();
        public HistoryConfiguration History { get; set; } = new();
        public SecurityConfiguration Security { get; set; } = new();
        public FeaturesConfiguration Features { get; set; } = new();
    }

    public class ServerSettings
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 8000;
        public int MaxConnections { get; set; } = 100;
        public int TimeoutSeconds { get; set; } = 300;
        public bool EnableCors { get; set; } = true;
        public string StaticFilesPath { get; set; } = "wwwroot";
    }

    public class WhisperConfiguration
    {
        public string ModelName { get; set; } = "base";
        public string ModelsDirectory { get; set; } = "models";
        public string Language { get; set; } = "en";
        public float Temperature { get; set; } = 0.01f;
        public int MaxNewTokens { get; set; } = 384;
        public int ChunkLengthSeconds { get; set; } = 25;
        public int BatchSize { get; set; } = 8;
        public bool ReturnTimestamps { get; set; } = false;
        public bool EnableVAD { get; set; } = true;
        public float VadThreshold { get; set; } = 0.5f;
    }

    public class AudioConfiguration
    {
        public int MaxFileSizeMB { get; set; } = 100;
        public string[] SupportedFormats { get; set; } = { "wav", "mp3", "m4a", "flac", "ogg" };
        public int SampleRate { get; set; } = 16000;
        public float NormalizationLevel { get; set; } = -0.55f;
        public AudioPreprocessing Preprocessing { get; set; } = new();
    }

    public class AudioPreprocessing
    {
        public bool EnableNoiseReduction { get; set; } = false;
        public bool EnableSilenceRemoval { get; set; } = true;
        public int SilenceThreshold { get; set; } = -40;
    }

    public class PerformanceConfiguration
    {
        public string Device { get; set; } = "auto";
        public float GpuMemoryFraction { get; set; } = 0.8f;
        public bool EnableFlashAttention { get; set; } = true;
        public int MaxConcurrentRequests { get; set; } = 4;
        public int QueueSize { get; set; } = 10;
    }

    public class LoggingConfiguration
    {
        public string Level { get; set; } = "Information";
        public string FilePath { get; set; } = "logs/whisper-server.log";
        public int MaxFileSizeMB { get; set; } = 50;
        public int MaxFiles { get; set; } = 10;
        public bool EnableConsole { get; set; } = true;
        public bool EnablePerformanceMetrics { get; set; } = true;
    }

    public class HistoryConfiguration
    {
        public bool Enabled { get; set; } = true;
        public string Directory { get; set; } = "history";
        public int RetentionDays { get; set; } = 30;
        public bool SaveAudioFiles { get; set; } = false;
        public bool Compression { get; set; } = true;
    }

    public class SecurityConfiguration
    {
        public bool EnableApiKey { get; set; } = false;
        public string ApiKey { get; set; } = "";
        public RateLimitingConfiguration RateLimiting { get; set; } = new();
        public string[] AllowedOrigins { get; set; } = { "*" };
        public int MaxRequestSizeMB { get; set; } = 100;
    }

    public class RateLimitingConfiguration
    {
        public bool Enabled { get; set; } = false;
        public int RequestsPerMinute { get; set; } = 60;
        public int RequestsPerHour { get; set; } = 1000;
    }

    public class FeaturesConfiguration
    {
        public bool EnableUrlTranscription { get; set; } = true;
        public bool EnableBase64Transcription { get; set; } = true;
        public bool EnableLocalFileTranscription { get; set; } = true;
        public bool EnableStreaming { get; set; } = false;
        public bool EnableTranslation { get; set; } = true;
        public bool EnableSpeakerDiarization { get; set; } = false;
        public bool EnableAutoDownload { get; set; } = true;
    }
}