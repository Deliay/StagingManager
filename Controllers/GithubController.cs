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
    public class GithubController : Controller
    {
        [HttpPost]
        public IActionResult Post([FromBody] GithubWebhook value)
        {
            GithubService.Instance.PassGithubWebhook(value);
            return Ok();
        }
    }
}
