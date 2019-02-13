using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    public struct ParametersAction
    {
        public string branch { get; set; }
        public string staging { get; set; }
    }

    public struct ParameterAction
    {
        public string name { get; set; }
        public string value { get; set; }
    }

    public struct ParameterActions
    {
        public ParameterAction[] parameters { get; set; }
    }

    public struct BuildWithParameters
    {
        public List<ParameterAction> parameter;
    }

    public class SimpleBuild
    {
        public int number { get; set; }
        public string url { get; set; }
    }

    public struct Build
    {
        public int number { get; set; }
        public string url { get; set; }
        public bool building { get; set; }
        public long timestamp { get; set; }
        public long estimatedDuration { get; set; }
        public long duration { get; set; }
        public string result { get; set; }
        public SimpleBuild previousBuild { get; set; }
        public SimpleBuild nextBuild { get; set; }
    }

    public struct Pipeline
    {
        public SimpleBuild[] builds { get; set; }
        public SimpleBuild lastBuild { get; set; }
        public SimpleBuild lastCompletedBuild { get; set; }
        public SimpleBuild lastFailedBuild { get; set; }
        public SimpleBuild lastStableBuild { get; set; }
        public SimpleBuild lastSuccessfulBuild { get; set; }
        public SimpleBuild lastUnsuccessfulBuild { get; set; }
        public int nextBuildNumber { get; set; }
    }

    public struct Jobs
    {
        public string name { get; set; }
        public string url { get; set; }
        public string color { get; set; }
    }

    public struct MainPage
    {
        public Jobs[] jobs { get; set; }
    }

    static class Extenstions
    {
        public static JObject ParseResult(this HttpResponseMessage msg)
        {
            var literal = msg.Content.ReadAsStringAsync().Result;
            try
            {
                return JObject.Parse(literal);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error " + e.Message + " In " + literal);
                return null;
            }
        }

        public static ParametersAction ToParameters(this ParameterAction[] actions)
        {

            var param = new ParametersAction();
            foreach (var action in actions)
            {
                switch (action.name)
                {
                    case "branch":
                        param.branch = action.value;
                        break;
                    case "staging":
                        param.staging = action.value;
                        break;
                }
            }
            return param;
        }
    }

    public struct JenkinsConfiguration
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string BaseURL { get; set; }
        public string Pipeline { get; set; }
    }

    public class JenkinsServices
    {
        public static readonly Dictionary<int, bool> BuildStatusCache = new Dictionary<int, bool>();
        public static readonly JenkinsServices Instance = new JenkinsServices();
        private string _jenkinsLastError = string.Empty;
        public JenkinsConfiguration JenkinsConfiguration;
        private readonly string ConfigurationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jenkins.json");
        private readonly bool IsStagingConfigurated = false;
        private readonly HttpClient HttpClient = new HttpClient();
        private readonly Dictionary<int, Build> BuildCache = new Dictionary<int, Build>();
        private readonly Dictionary<int, ParametersAction> ParameterCache = new Dictionary<int, ParametersAction>();
        private readonly DateTime UNIX = new DateTime(1970, 1, 1, 0, 0, 0).AddHours(8);
        private string crumbIssuer = "";
        private string crumbField = "";
        private Pipeline Pipeline;
        private JenkinsServices()
        {
            if (File.Exists(ConfigurationPath))
            {
                try
                {
                    JenkinsConfiguration = (JenkinsConfiguration)JsonConvert.DeserializeObject(File.ReadAllText(ConfigurationPath), typeof(JenkinsConfiguration));
                    if (JenkinsConfiguration.Pipeline.Length == 0)
                    {
                        _jenkinsLastError = $"目前仅支持单Pipeline，请在配置文件中指定Pipeline";
                        return;
                    }
                    string cred = $"{JenkinsConfiguration.UserName}:{JenkinsConfiguration.Password}";
                    Console.WriteLine($"Basic {cred}");
                    HttpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(cred)));

                    var crumb = HttpClient.GetAsync(Request("crumbIssuer")).Result.ParseResult();
                    crumbIssuer = crumb["crumb"].ToString();
                    crumbField = crumb["crumbRequestField"].ToString();

                    HttpClient.DefaultRequestHeaders.Add(crumbField, crumbIssuer);
                    HttpClient.DefaultRequestHeaders.Add("crumb", crumbIssuer);

                    using (var result = HttpClient.GetAsync(Request("")).Result)
                    {
                        var main = result.ParseResult().ToObject<MainPage>();
                        if (main.jobs.Length == 0 || main.jobs.All(p => p.name != JenkinsConfiguration.Pipeline))
                        {
                            _jenkinsLastError = "配置文件中Pipeline与实际环境不一致";
                            return;
                        }
                    }
                    HttpClient.GetAsync(Request(""));
                    if (!RemindService.Instance.PostUri.ContainsKey("Jenkins"))
                    {
                        _jenkinsLastError = "提醒服务中尚未配置Jenkins字段，无法使用 Staging部署机器人";
                        return;
                    }
                    IsStagingConfigurated = true;
                    Console.WriteLine("Jenkins initializtion complete!");
                }
                catch (Exception e)
                {
                    JenkinsConfiguration = new JenkinsConfiguration();
                    _jenkinsLastError = $"在登录Jenkins过程中出现了偏差: {e.Message}";
                }
            }
            else
            {
                JenkinsConfiguration = new JenkinsConfiguration();
            }
        }
        private readonly Random random = new Random();
        private string Request(string param, bool makeAPI = true) => JenkinsConfiguration.BaseURL + param + (makeAPI ? "/api/json" : "") + $"?_={random.Next()}";
        private string BuildLink(string id) => $"{JenkinsConfiguration.BaseURL}job/{JenkinsConfiguration.Pipeline}/{id}";

        public void GetBuild(int id)
        {
            using (var result = HttpClient.GetAsync(Request($"job/{JenkinsConfiguration.Pipeline}/{id}")).Result)
            {
                var json = result.ParseResult();
                var param = json["actions"].FirstOrDefault(t => t.HasValues && t["_class"].ToString() == "hudson.model.ParametersAction").ToObject<ParameterActions>();

                if (BuildCache.ContainsKey(id))
                {
                    BuildCache[id] = json.ToObject<Build>();
                    ParameterCache[id] = param.parameters.ToParameters();
                }
                else
                {
                    BuildCache.Add(id, json.ToObject<Build>());
                    ParameterCache.Add(id, param.parameters.ToParameters());
                }
                // mean in building
                if (BuildCache[id].building)
                {
                    BuildStatusCache[id] = true;
                    Console.WriteLine($"#{id} still building...");
                }
                // mean tracking by this service(status TRUE), and build complete
                if (BuildStatusCache.ContainsKey(id) && BuildStatusCache[id] && !BuildCache[id].building)
                {
                    var stagingId = int.Parse(ParameterCache[id].staging.Substring(7));
                    var staging = StagingService.Instance.GetStaging(stagingId);
                    BuildStatusCache[id] = false;
                    Console.WriteLine($"Build {id} seem complete!");
                    // success
                    string status = "部署失败";
                    if (BuildCache[id].result == "SUCCESS")
                    {
                        status = "已部署完成";
                    }
                    RemindService.Instance.SendMessage($"@{staging.Owner} 你的部署任务[#{id}]({BuildLink(id.ToString())}) `{ParameterCache[id].branch}`->`{ParameterCache[id].staging}` {status}。");
                }
            }
        }

        public void PeekWhileNotInBuild()
        {
            foreach (var build in Pipeline.builds)
            {
                GetBuild(build.number);
                if (!BuildCache[build.number].building) break;
            }

            var needUpdate = BuildCache.Where(p => p.Value.building).Select(p => p.Value.number).ToList();
            foreach (var number in needUpdate) GetBuild(number);
        }

        public void GetPipeline()
        {
            using (var result = HttpClient.GetAsync(Request($"job/{JenkinsConfiguration.Pipeline}")).Result)
            {
                Pipeline = result.ParseResult().ToObject<Pipeline>();
            }
            if (!BuildCache.ContainsKey(Pipeline.lastBuild.number))
                GetBuild(Pipeline.lastBuild.number);
        }

        public string GetMainPanel(string owner)
        {
            if (!IsStagingConfigurated)
            {
                return $"Jenkins错误 {_jenkinsLastError}";
            }
            Task.Run(() =>
            {
                StringBuilder sb = new StringBuilder();
                GetPipeline();
                PeekWhileNotInBuild();
                sb.AppendLine($"Jenkins {JenkinsConfiguration.Pipeline} 流程");
                var lastBuildParam = ParameterCache[Pipeline.lastBuild.number];
                var lastBuild = BuildCache[Pipeline.lastBuild.number];
                if (lastBuild.building) sb.AppendLine($"状态：`Building` `{lastBuildParam.branch}`->`{lastBuildParam.staging}({lastBuild.estimatedDuration / 1000:N2}s)`");
                else sb.AppendLine($"状态：`Idle` 上次部署：`{lastBuildParam.branch}`->`{lastBuildParam.staging}`(`{lastBuild.result}`)");
                var allStagingIds = StagingService.Instance.GetAllStaging()
                    .Where(s => StagingService.Instance.IsStagingInUse(s) && s.Owner == owner)
                    .Select(s => s.StagingId).ToList();
                sb.AppendLine("---");
                bool isBuilding = false;
                var allBuilding = BuildCache.Where(b => b.Value.building);
                foreach (var build in BuildCache.Where(b => b.Value.building))
                {
                    isBuilding = true;
                    var duration = DateTime.Now - UNIX.AddMilliseconds(build.Value.timestamp);
                    var percentDone = duration.TotalMilliseconds / build.Value.estimatedDuration * 100;
                    var allDuration = TimeSpan.FromMilliseconds(build.Value.estimatedDuration);
                    var param = ParameterCache[build.Value.number];
                    sb.AppendLine($"Build [#{build.Value.number}]({BuildLink(build.Value.number.ToString())})： `{param.branch}`->`{param.staging}` ({percentDone:N2}%, {duration.TotalMinutes:N2}/{allDuration.TotalMinutes:N2}min)");
                }
                if (!isBuilding) sb.AppendLine("当前没有任务正在Build");
                sb.AppendLine("---");
                var allStagingIdsPerfix = allStagingIds.Select(s => $"staging{s}");
                var hasBuilding = allBuilding.Any(p => allStagingIdsPerfix.Contains(ParameterCache[p.Key].staging));
                if (allStagingIds.Count > 0)
                {
                    if (!hasBuilding)
                    {
                        sb.AppendLine($"你当前占用的是staging {string.Join('、', allStagingIds)}");
                        sb.AppendLine($"示例：`!staging jenkins b {allStagingIds[0]} master`将master部署到staging{allStagingIds[0]}上");
                    }
                    else
                    {
                        sb.AppendLine("你的部署任务正在进行中...");
                        sb.AppendLine("使用`!staging jenkins stop`取消你的这次任务");
                    }
                }
                else
                {
                    sb.AppendLine($"你当前没有占用Staging，无法使用本机器人进行部署");
                    sb.AppendLine($"需要帮助，请输入`!staging help`");
                }

                RemindService.Instance.SendMessage(sb.ToString(), "Jenkins");
            });
            return string.Empty;
        }

        public string Build(string owner, string staging, string branch)
        {
            var intStaging = int.Parse(staging);
            if (StagingService.Instance.GetStaging(intStaging).Owner != owner)
            {
                return $"@{owner} 这个staging不是你在占用~ 请先占用。如果仍需要部署请前往 [{JenkinsConfiguration.Pipeline}]({JenkinsConfiguration.BaseURL}job/{JenkinsConfiguration.Pipeline})部署";
            }
            var fullStagingName = $"staging{staging}";
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("name", "branch"),
                new KeyValuePair<string, string>("value", branch),
                new KeyValuePair<string, string>("name", "deployer"),
                new KeyValuePair<string, string>("value", owner),
                new KeyValuePair<string, string>("name", "staging"),
                new KeyValuePair<string, string>("value", fullStagingName),
                new KeyValuePair<string, string>("name", "ignore_lint"),
                new KeyValuePair<string, string>("value", "true"),
                new KeyValuePair<string, string>("name", "without_sourcemap"),
                new KeyValuePair<string, string>("value", "true"),
                new KeyValuePair<string, string>("name", "update_env"),
                new KeyValuePair<string, string>("value", "false"),
                new KeyValuePair<string, string>("json", JObject.FromObject(new BuildWithParameters() {
                    parameter = new List<ParameterAction>() {
                        new ParameterAction() { name = "branch", value = branch },
                        new ParameterAction() { name = "deployer", value = owner },
                        new ParameterAction() { name = "staging", value = fullStagingName },
                        new ParameterAction() { name = "ignore_lint", value = "true" },
                        new ParameterAction() { name = "without_sourcemap", value = "true" },
                        new ParameterAction() { name = "update_env", value = "false" },
                    },
                }).ToString()),
                new KeyValuePair<string, string>(crumbField, crumbIssuer),
            });
            Task.Run(() =>
            {
                GetPipeline();
                PeekWhileNotInBuild();
                if (BuildCache.Any(p => p.Value.building && ParameterCache[p.Key].staging == fullStagingName))
                {
                    var build = BuildCache.First(p => p.Value.building && ParameterCache[p.Key].staging == fullStagingName);
                    RemindService.Instance.SendMessage($"@{owner} 这个staging已经正在部署了，点此 [查看部署进度]({JenkinsConfiguration.BaseURL}job/{JenkinsConfiguration.Pipeline}/{build.Value.number})", "Jenkins");
                    return;
                }
                string ret = string.Empty;
                using (var result = HttpClient.PostAsync(Request($"job/{JenkinsConfiguration.Pipeline}/build"), content).Result)
                {
                    Console.WriteLine(result.ToString());
                    Console.WriteLine(HttpClient.DefaultRequestHeaders.ToString());
                    if (result.StatusCode == HttpStatusCode.Created)
                    {
                        ret = $"@{owner} 部署任务 `{branch}`->`{fullStagingName}` 添加成功 :tada:";
                    }
                    else ret = $"@{owner} 部署任务 `{branch}`->`{fullStagingName}` 添加失败!! ({result.StatusCode.ToString()})";
                };
                RemindService.Instance.SendMessage(ret, "Jenkins");
            });
            return "部署请求已经发送。";
        }

        public string StopBuild(string owner)
        {
            Task.Run(() =>
            {
                // fetch building progress fist
                PeekWhileNotInBuild();
                var ret = string.Empty;
                var allStagingIds = StagingService.Instance.GetAllStaging()
                .Where(s => StagingService.Instance.IsStagingInUse(s) && s.Owner == owner)
                .Select(s => $"staging{s.StagingId}").ToList();

                var allBuilding = BuildCache.Where(b => b.Value.building);
                foreach (var pair in allBuilding)
                {
                    var param = ParameterCache[pair.Key];
                    // 找到了任意一个staging属于发送命令的owner
                    if (allStagingIds.Contains(ParameterCache[pair.Key].staging))
                    {
                        if (StopBuild(pair.Key))
                        {
                            ret = $"@{owner} 成功结束了Build #{pair.Value.number}, `{param.branch}`->`{param.staging}`";
                        }
                        else
                        {
                            ret = $"@{owner} 任务Build #{pair.Value.number} 结束失败。";
                        }
                    }
                }
                if (ret == string.Empty) ret = $"@{owner} 没有找到你可以结束的任务。";
                RemindService.Instance.SendMessage(ret, "Jenkins");
            });
            return string.Empty;
        }

        public bool StopBuild(int number)
        {
            using (var res = HttpClient.PostAsync(Request($"job/{JenkinsConfiguration.Pipeline}/{number.ToString()}/stop"), null).Result)
            {
                var code = res.StatusCode;
                return code == HttpStatusCode.Forbidden || code == HttpStatusCode.Found || code == HttpStatusCode.OK;
            }
        }
    }
}
