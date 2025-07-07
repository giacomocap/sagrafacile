using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging; // Added for logging
using System; // Added for Exception handling

namespace SagraFacile.NET.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InstanceController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<InstanceController> _logger;

        public InstanceController(IConfiguration configuration, ILogger<InstanceController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Gets information about the running instance, such as its mode.
        /// </summary>
        /// <returns>An object containing instance information.</returns>
        [AllowAnonymous]
        [HttpGet("info")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetInstanceInfo()
        {
            _logger.LogInformation("Received request for instance info.");
            try
            {
                var appMode = (_configuration["APP_MODE"] ?? _configuration["AppSettings:AppMode"])?.ToLower() ?? "opensource";
                _logger.LogInformation("Instance mode determined as '{AppMode}'.", appMode);
                return Ok(new { mode = appMode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting instance info.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
