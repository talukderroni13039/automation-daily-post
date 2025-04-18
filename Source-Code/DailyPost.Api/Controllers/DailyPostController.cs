using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Xml.Linq;
using System.IO;
using OpenQA.Selenium.BiDi.Modules.Script;
using System.Runtime.Intrinsics.Arm;
using DailyPost.BackgroundWorker;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace DailyPost.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DailyPostController : ControllerBase
    {    
        [HttpPost("CreateMessage")]
        public async Task<IActionResult> Post([FromBody] Message message)
        {
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
