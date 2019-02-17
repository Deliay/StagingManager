using CheckStaging.Models;
using CheckStaging.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CheckStaging.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CircleCIController : Controller
    {
        [HttpPost]
        public IActionResult Post([FromBody] CircleCIWebhookPayload value)
        {
            CircleCIServices.PassCircleCIWebhook(value.payload);
            return Ok();
        }

        [HttpPost("bc")]
        public IActionResult BearyChatPost([FromBody] Incoming value)
        {
            if (value.token != "11f76f998feac74f9bb6de5bab293ed1") return Forbid("Auth Fail");
            if (value.channel_name != "CI挂没挂" && value.channel_name != "Staging占坑测试频道") return Forbid("Wrong channel");
            if (value.user_name == null || value.user_name.Length == 0) return Forbid("User name error");
            return Ok(ChannelCommandService.Instance.PassIncoming(value));
        }
    }
}
