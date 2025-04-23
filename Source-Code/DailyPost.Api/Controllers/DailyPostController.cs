using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using DailyPost.BackgroundWorker;
using DailyPost.BackgroundWorker.Services;
using Serilog;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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

        [HttpPost("PostDailyStatus")]
        public async Task<IActionResult> Post([FromBody] Message message)
        {
            try
            {
                Log.Information("Data Comming From  api");

                string projectRoot = Path.Combine(Directory.GetCurrentDirectory(), "Message");
                string filePath = Path.Combine(projectRoot, "message.json");
                var jsonData = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });

                // Make sure the directory exists
                Directory.CreateDirectory(projectRoot);
                await System.IO.File.WriteAllTextAsync(filePath, jsonData);
                Log.Information("Data from API has been saved to JSON file.");

                return Ok(message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception Occured " + ex.Message);
                return Problem( detail: "An error occurred while saving the message data", title: "Internal Server Error",  statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
