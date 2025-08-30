using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("v1/models")]
public class ModelsController : ControllerBase
{
    // Static list for MVP; later tie to ModelManager
    private static readonly string[] AvailableModelIds = new[]
    {
        "whisper-tiny",
        "whisper-base",
        "whisper-small",
        "whisper-medium",
        "whisper-large-v3"
    };

    [HttpGet]
    public IActionResult GetModels()
    {
        var data = AvailableModelIds.Select(id => new ModelInfo
        {
            Id = id,
            Object = "model",
            OwnedBy = "openai"
        }).ToArray();

        return Ok(new ModelsList
        {
            Object = "list",
            Data = data
        });
    }

    [HttpGet("{id}")]
    public IActionResult GetModelById(string id)
    {
        if (!AvailableModelIds.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            return NotFound(new ErrorEnvelope
            {
                Error = new ErrorBody
                {
                    Message = $"Model '{id}' does not exist",
                    Type = "invalid_request_error",
                    Param = "id",
                    Code = "model_not_found"
                }
            });
        }

        return Ok(new ModelInfo
        {
            Id = AvailableModelIds.First(m => string.Equals(m, id, StringComparison.OrdinalIgnoreCase)),
            Object = "model",
            OwnedBy = "openai"
        });
    }

    public sealed class ModelsList
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";

        [JsonPropertyName("data")]
        public ModelInfo[] Data { get; set; } = Array.Empty<ModelInfo>();
    }

    public sealed class ModelInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")]
        public string Object { get; set; } = "model";

        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; set; } = "openai";
    }

    public sealed class ErrorEnvelope
    {
        [JsonPropertyName("error")]
        public ErrorBody Error { get; set; } = new();
    }

    public sealed class ErrorBody
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "invalid_request_error";

        [JsonPropertyName("param")]
        public string? Param { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}
