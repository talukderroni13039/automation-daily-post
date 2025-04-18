using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Xml.Linq;
using System.IO;
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

            var _filePath = "Message.json";
            var jsonData = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });
            if (System.IO.File.Exists(_filePath))
            {
                await System.IO.File.WriteAllTextAsync(_filePath, jsonData);
            }
            return Ok(message);
        }
    }
}
