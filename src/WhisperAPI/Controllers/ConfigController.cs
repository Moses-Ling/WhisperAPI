using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhisperAPI.Configuration;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("config")]
[Route("v1/config")]
public class ConfigController : ControllerBase
{
    private readonly AppConfiguration _config;

    public ConfigController(IOptions<AppConfiguration> options)
    {
        _config = options.Value;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        return Ok(_config);
    }
}
