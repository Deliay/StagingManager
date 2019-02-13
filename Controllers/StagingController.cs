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
            if (value.token != "8d6f5328399d242c18169b55a4c18a79")
            {
                return Forbid("Auth Fail");
            }
            if (value.channel_name != "Staging占坑测试频道" && value.channel_name != "staging")
            {
                return Forbid("Channel error");
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
