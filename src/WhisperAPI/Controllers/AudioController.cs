using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using WhisperAPI.Configuration;
using WhisperAPI.Filters;
using WhisperAPI.Services;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("v1/audio")] 
public sealed class AudioController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".wav", ".mp3", ".m4a", ".flac", ".ogg" };

    private readonly AppConfiguration _config;
    private readonly ILogger<AudioController> _logger;

    public AudioController(IOptions<AppConfiguration> options, ILogger<AudioController> logger)
    {
        _config = options.Value;
        _logger = logger;
    }

    [HttpPost("transcriptions")]
    [ServiceFilter(typeof(ConcurrencyLimiterFilter))]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<IActionResult> Transcribe([FromServices] AudioValidationService audioValidator, [FromServices] WhisperTranscriber transcriber)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(OpenAiError("Request must be multipart/form-data"));
        }

        var form = await Request.ReadFormAsync();
        var file = form.Files.GetFile("file");
        var model = form["model"].FirstOrDefault() ?? _config.Whisper.ModelName;
        var language = form["language"].FirstOrDefault() ?? _config.Whisper.Language;

        if (file is null || file.Length == 0)
        {
            return BadRequest(OpenAiError("No audio file provided", code: "invalid_request_error"));
        }

        if (file.Length > (long)_config.Audio.MaxFileSizeMb * 1024 * 1024)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, OpenAiError("File too large", code: "file_too_large"));
        }

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, OpenAiError($"Unsupported media type: {ext}", code: "unsupported_media_type"));
        }

        // Normalize audio to 16kHz mono WAV
        await using var stream = file.OpenReadStream();
        string wavPath;
        try
        {
            wavPath = await audioValidator.NormalizeToWav16kMonoAsync(stream, file.FileName, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio normalization failed");
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, OpenAiError("Failed to process audio", code: "audio_processing_failed"));
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.Server.TimeoutSeconds));
            var result = await transcriber.TranscribeAsync(wavPath, cts.Token);
            try { System.IO.File.Delete(wavPath); } catch { }
            return Ok(new
            {
                text = result.Text,
                duration = result.Duration,
                language = result.Language,
                segments = result.Segments.Select(s => new { id = s.Id, start = s.Start, end = s.End, text = s.Text })
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Transcription failed: model not ready");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, OpenAiError("Model not ready. Please download or configure the model.", type: "server_error", code: "model_not_ready"));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, OpenAiError("Transcription timed out", type: "request_timeout", code: "timeout"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return StatusCode(StatusCodes.Status500InternalServerError, OpenAiError("Internal server error", type: "server_error"));
        }
    }

    [HttpPost("transcriptions/base64")]
    [ServiceFilter(typeof(ConcurrencyLimiterFilter))]
    public async Task<IActionResult> TranscribeBase64(
        [FromBody] Base64TranscriptionRequest body,
        [FromServices] AudioValidationService audioValidator,
        [FromServices] WhisperTranscriber transcriber)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Audio))
        {
            return BadRequest(OpenAiError("Missing 'audio' base64 field", code: "invalid_request_error"));
        }

        var fileName = string.IsNullOrWhiteSpace(body.Filename) ? "audio.wav" : body.Filename!;
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(body.Audio);
        }
        catch
        {
            return BadRequest(OpenAiError("Invalid base64 data", code: "invalid_base64"));
        }
        if (bytes.LongLength > (long)_config.Audio.MaxFileSizeMb * 1024 * 1024)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, OpenAiError("File too large", code: "file_too_large"));
        }

        await using var inputStream = new MemoryStream(bytes, writable: false);
        string wavPath;
        try
        {
            wavPath = await audioValidator.NormalizeToWav16kMonoAsync(inputStream, fileName, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio normalization failed (base64)");
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, OpenAiError("Failed to process audio", code: "audio_processing_failed"));
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.Server.TimeoutSeconds));
            var result = await transcriber.TranscribeAsync(wavPath, cts.Token);
            try { System.IO.File.Delete(wavPath); } catch { }
            return Ok(new
            {
                text = result.Text,
                duration = result.Duration,
                language = result.Language,
                segments = result.Segments.Select(s => new { id = s.Id, start = s.Start, end = s.End, text = s.Text })
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, OpenAiError("Transcription timed out", type: "request_timeout", code: "timeout"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed (base64)");
            return StatusCode(StatusCodes.Status500InternalServerError, OpenAiError("Internal server error", type: "server_error"));
        }
    }

    [HttpPost("transcriptions/url")]
    [ServiceFilter(typeof(ConcurrencyLimiterFilter))]
    public async Task<IActionResult> TranscribeUrl(
        [FromBody] UrlTranscriptionRequest body,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] AudioValidationService audioValidator,
        [FromServices] WhisperTranscriber transcriber)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Url))
        {
            return BadRequest(OpenAiError("Missing 'url' field", code: "invalid_request_error"));
        }

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(15, _config.Server.TimeoutSeconds + 10));

        string tempPath = Path.Combine(Path.GetTempPath(), "whisperapi", Guid.NewGuid() + ".bin");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, body.Url);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
            if (!resp.IsSuccessStatusCode)
            {
                return StatusCode((int)resp.StatusCode, OpenAiError($"Failed to download audio from URL ({(int)resp.StatusCode})", code: "url_fetch_failed"));
            }
            var contentLen = resp.Content.Headers.ContentLength;
            var maxBytes = (long)_config.Audio.MaxFileSizeMb * 1024 * 1024;
            if (contentLen.HasValue && contentLen.Value > maxBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge, OpenAiError("File too large", code: "file_too_large"));
            }

            await using (var fs = System.IO.File.Create(tempPath))
            await using (var stream = await resp.Content.ReadAsStreamAsync(HttpContext.RequestAborted))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), HttpContext.RequestAborted)) > 0)
                {
                    total += read;
                    if (total > maxBytes)
                    {
                        return StatusCode(StatusCodes.Status413PayloadTooLarge, OpenAiError("File too large", code: "file_too_large"));
                    }
                    await fs.WriteAsync(buffer.AsMemory(0, read), HttpContext.RequestAborted);
                }
            }

            await using var inputStream = System.IO.File.OpenRead(tempPath);
            var finalName = string.IsNullOrWhiteSpace(body.Filename) ? "audio.wav" : body.Filename!;
            var wavPath = await audioValidator.NormalizeToWav16kMonoAsync(inputStream, finalName, HttpContext.RequestAborted);
            try { System.IO.File.Delete(tempPath); } catch { }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.Server.TimeoutSeconds));
            var result = await transcriber.TranscribeAsync(wavPath, cts.Token);
            try { System.IO.File.Delete(wavPath); } catch { }
            return Ok(new
            {
                text = result.Text,
                duration = result.Duration,
                language = result.Language,
                segments = result.Segments.Select(s => new { id = s.Id, start = s.Start, end = s.End, text = s.Text })
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, OpenAiError("Transcription timed out", type: "request_timeout", code: "timeout"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed (url)");
            return StatusCode(StatusCodes.Status500InternalServerError, OpenAiError("Internal server error", type: "server_error"));
        }
        finally
        {
            try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
        }
    }

    public sealed class Base64TranscriptionRequest
    {
        [JsonPropertyName("audio")] public string Audio { get; set; } = string.Empty;
        [JsonPropertyName("filename")] public string? Filename { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("language")] public string? Language { get; set; }
    }

    public sealed class UrlTranscriptionRequest
    {
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
        [JsonPropertyName("filename")] public string? Filename { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("language")] public string? Language { get; set; }
    }

    private static object OpenAiError(string message, string type = "invalid_request_error", string? code = null, string? param = null)
    {
        return new
        {
            error = new
            {
                message,
                type,
                param,
                code
            }
        };
    }
}
