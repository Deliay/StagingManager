using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CheckStaging.Models;
using CheckStaging.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace CheckStaging.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StagingController : Controller
    {
        [HttpPost]
        public IActionResult Post([FromBody] Incoming value)
        {
            if (value.token != "8d6f5328399d242c18169b55a4c18a79" && value.token != "247b43e123512ea396aed10074fc0e35")
            {
                return Forbid("Auth Fail");
            }
            if (value.channel_name != "Staging占坑测试频道" && value.channel_name != "staging" && value.channel_name != "倍洽小助手")
            {
                return Forbid($"{value.channel_name} is not a valid channel");
            }
            if (value.user_name == null || value.user_name.Length == 0)
            {
                return Forbid("User name error");
            }
            var result = StagingCommandService.Instance.PassIncoming(value);
            if (result.text == string.Empty) return Ok();
            return Ok(result);
        }
    }
}
