using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using DailyPost.BackgroundWorker;
using DailyPost.BackgroundWorker.Services;
using Serilog;
using Microsoft.AspNetCore.Mvc.RazorPages;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace DailyPost.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DailyPostController : ControllerBase
    {

        private readonly ILogger<DailyPostController> _logger;

        public DailyPostController(ILogger<DailyPostController> logger, IDailyPostService dailyPostService, IConfiguration configuration)
        {
            _logger = logger;
        }

        [HttpPost("CreateMessage")]
        public async Task<IActionResult> Post([FromBody] Message message)
        {

            Log.Information("Message1 ");
            Log.Error($"Message1");

            var projectRoot = Path.Combine(Directory.GetCurrentDirectory(), "Message");
            string filePath = Path.Combine(projectRoot, "Message.json");
            var jsonData = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });
          
            Directory.CreateDirectory(projectRoot); 
            if (System.IO.File.Exists(filePath))
            {
                await System.IO.File.WriteAllTextAsync(filePath, jsonData);
            }
            return Ok(message);
        }
    }
}
