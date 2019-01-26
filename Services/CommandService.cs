using CheckStaging.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    public struct Command
    {
        public string CommandMsg { get; set; }
        public string CommandArgs { get; set; }
        public string Channel { get; set; }
        public string Owner { get; set; }
        public string Message { get; set; }
        public string RawMessage { get; set; }
    }

    public class CommandService
    {
        public static readonly CommandService Instance = new CommandService();
        private readonly Dictionary<string, Func<Command, Outgoing>> _commandExecutor = new Dictionary<string, Func<Command, Outgoing>>();

        public Outgoing Out(string text) => new Outgoing() { text = text };

        public string SkipSpace(string str) => str.Trim();

        public Outgoing Capture(Command args)
        {
            Console.WriteLine(args.CommandArgs);
            int[] preferStaging = new int[0];
            string next = args.CommandArgs;
            if (args.CommandArgs.StartsWith('['))
            {
                var mulStag = next.Substring(1, args.CommandArgs.IndexOf(']') - 1);
                //Multi staging
                preferStaging = mulStag
                    .Split(',').Select(int.Parse)
                    .ToArray();

                next = next.Substring(mulStag.Length + 2).Trim();
            }
            var time = next.Length > 0 ? int.Parse(next) : 3;
            var (staging, queueTask, err) = StagingService.Instance.CaptureStaging(args.Owner, time, preferStaging);
            if (staging != null)
            {
                return Out($"@{args.Owner} 占坑成功，你的Staging是S{staging.StagingId}，共{staging.Timeleft}天。");
            }
            if (queueTask != null)
            {
                return Out($"@{args.Owner}，Staging满了，占坑请求已被加入QueueTask豪华午餐，当有人释放Staging，我会通知你");
            }
            if (err != string.Empty)
            {
                return Out($"@{args.Owner} {err}");
            }
            return Out($"@龙轩 发生了意外情况？");
        }

        public Outgoing Status(Command args)
        {
            StringBuilder sb = new StringBuilder();
            bool isAll = args.CommandArgs == "all" || args.CommandMsg == "all";
            bool isIdle = args.CommandArgs == "idle";
            int idleCount = StagingService.Instance.IdleStagingCount();
            bool hasStaging = false, hasTask = false;
            string getStatusStaging(Staging s) => $"{s.Owner}，剩余{(s.StartTime.AddDays(s.Timeleft) - DateTime.Today).TotalDays}天";
            string getStatusTask(QueueTask t) => t.PreferStaging.Length > 0 ? $"S{string.Join('、', t.PreferStaging)}" : "任意Staging";
            sb.AppendLine($"**Staging** （空闲：{idleCount}/{StagingService.MAX_STAGING_COUNT}个）");
            if (isAll == false) sb.AppendLine("你目前在占用的Staging: ");
            foreach (var staging in StagingService.Instance.AllStaging.Stagings)
            {
                if (!isAll && staging.Owner != args.Owner) continue;

                if (StagingService.Instance.IsStagingInUse(staging.StagingId))
                {
                    sb.AppendLine($"**Staging{staging.StagingId}** {getStatusStaging(staging)}");
                    hasStaging = true;
                }
            }
            if (!hasStaging) sb.AppendLine(">当前没有在使用的Staging");
            sb.AppendLine();

            if (idleCount > 0)
                sb.AppendLine($"空闲Staging: {string.Join('、', StagingService.Instance.IdleStagings().Select(s => s.StagingId))}");
            else
                sb.AppendLine("当前没有剩余的Staging了");

            sb.AppendLine("**Task Queue**");
            foreach (var task in StagingService.Instance.AllStaging.QueueTasks)
            {
                sb.AppendLine($"> {task.Owner} 排 `{getStatusTask(task)}` `{task.Timeleft}`天");
                hasTask = true;
            }
            if (!hasTask) sb.AppendLine(">当前没有人排队");
            sb.AppendLine();
            sb.AppendLine("如需帮助，请输入`!staging help`");
            if (!isAll) sb.AppendLine("如需查看所有Staging占用情况，请使用`!staging status all`");
            return Out(sb.ToString());
        }

        public Outgoing Release(Command args)
        {
            int[] releaseStaging = new int[0];
            string next = args.CommandArgs.Trim();
            if (args.CommandArgs.Length == 0)
            {
                return Out("参数错误，请输入`!staging help`查看帮助");
            }
            if (next.StartsWith('['))
            {
                var mulStag = next.Substring(1, next.IndexOf(']') - 1);
                releaseStaging = mulStag
                    .Split(',').Select(int.Parse)
                    .ToArray();
            }
            else
            {
                releaseStaging = new[] { int.Parse(next.Trim()) };
            }
            var (task, staging, removedStaging) = StagingService.Instance.ReleaseStaging(args.Owner, releaseStaging);
            StringBuilder sb = new StringBuilder();
            if (removedStaging.Count() > 0)
            {
                sb.AppendLine($"@{args.Owner} 成功释放Staging{string.Join('、', releaseStaging)}！");
            }
            else
            {
                sb.AppendLine($"@{args.Owner} 你的操作并没有释放任何Staging");
            }
            if (task != null)
            {
                sb.AppendLine($"@{task.Owner}！现在自动为你占了Staging{staging.StagingId}，共{staging.Timeleft}天");
            }
            return Out(sb.ToString());
        }

        public Outgoing Cancel(Command args)
        {
            StagingService.Instance.CancelAllTask(args.Owner);
            return Out($"@{args.Owner} 你的所有排队都已取消");
        }

        public Outgoing Renew(Command args)
        {
            int[] renewStaging = new int[0];
            string next = args.CommandArgs.Trim();
            if (args.CommandArgs.Length == 0)
            {
                return Out("参数错误，请输入`!staging help`查看帮助");
            }
            if (next.StartsWith('['))
            {
                var mulStag = next.Substring(1, next.IndexOf(']') - 1);
                renewStaging = mulStag
                    .Split(',').Select(int.Parse)
                    .ToArray();
            }
            else
            {
                renewStaging = new[] { int.Parse(next.Trim()) };
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"@{args.Owner}：");
            foreach (var stagingId in renewStaging)
            {
                if (StagingService.Instance.RenewStaging(args.Owner, stagingId))
                {
                    sb.AppendLine($"> Staging{stagingId} 成功续了1天");
                }
                else
                {
                    sb.AppendLine($"> Staging{stagingId} 续命失败! 请闷声发大财。");
                }
            }
            return Out(sb.ToString());
        }

        public Outgoing Help(Command args)
        {
            StringBuilder sb = new StringBuilder();
            sb
                .AppendLine("这里是Staging占坑机器人~ :wink: ")
                .AppendLine("---")
                .AppendLine("**命令列表**")
                .AppendLine("`!staging capture [可选指定机器列表] 天数`: 占用Staging")
                .AppendLine("`!staging release [多个机器] 单个机器`: 释放Staging")
                .AppendLine("`!staging renew [多个机器] 单个机器`: 续期Staging 1天")
                .AppendLine("`!staging integration`: 自动征用S2进行集成测试1天")
                .AppendLine("`!staging cancel`: 取消自己所有排队")
                .AppendLine("`!staging status`: 当前Staging占用情况")
                .AppendLine("`!staging all`: 所有Staging占用情况")
                .AppendLine("`!staging help`: 查看帮助")
                .AppendLine("`!staging jenkins`: 查看Jenkins相关帮助")
                .AppendLine("---")
                .AppendLine("`!staging capture 4`: 希望占`任意Staging` 一共4天")
                .AppendLine("`!staging capture [5,6] 4`: 希望占用`Staging5或6` 一共4天")
                .AppendLine("---")
                .AppendLine("**额外说明**")
                .AppendLine("1. 如需占多个机器，请指定Staging进行占用")
                .AppendLine("2. 请及时续期，如果到期，将会立即把Staging让给在里Queue的同学")
                .AppendLine("3. 特殊的s3/s9/s15将会被系统标记为占用状态，无法在本机器人处进行占用")
                .AppendLine("4. 每周二四五集成测试，可以直接使用`!staging i`命令征用s2")
                .AppendLine("5. 每天10、14、18点都会提醒owner只剩1天的staging。");

            return Out(sb.ToString());
        }

        public Outgoing Integration(Command args)
        {
            if (StagingService.Instance.Integration())
            {
                return Out($"@{args.Owner}，集成测试分支已释放.");
            }
            else
            {
                return Out($"@{args.Owner}，集成测试分支已占用.");
            }
        }

        public Outgoing Jenkins(Command args)
        {
            var splitArgs = args.CommandArgs.Split(' ', 3);
            if (args.CommandArgs.StartsWith("stop"))
            {
                return new Outgoing() { text = JenkinsServices.Instance.StopBuild(args.Owner) };
            }
            else if (args.CommandArgs.StartsWith("b"))
            {
                if (splitArgs.Length == 3)
                {
                    return new Outgoing() { text = JenkinsServices.Instance.Build(args.Owner, splitArgs[1], splitArgs[2]) };
                }
                else
                {
                    return new Outgoing() { text = $"部署参数错误" };
                }
            }
            return new Outgoing() { text = JenkinsServices.Instance.GetMainPanel(args.Owner) };
        }

        private CommandService()
        {
            _commandExecutor.Add("capture", Capture);
            _commandExecutor.Add("c", Capture);

            _commandExecutor.Add("status", Status);
            _commandExecutor.Add("s", Status);
            _commandExecutor.Add("all", Status);

            _commandExecutor.Add("release", Release);
            _commandExecutor.Add("r", Release);

            _commandExecutor.Add("renew", Renew);
            _commandExecutor.Add("n", Renew);

            _commandExecutor.Add("help", Help);
            _commandExecutor.Add("h", Help);

            _commandExecutor.Add("cancel", Cancel);
            _commandExecutor.Add("x", Cancel);

            _commandExecutor.Add("integration", Integration);
            _commandExecutor.Add("i", Integration);

            _commandExecutor.Add("jenkins", Jenkins);
            _commandExecutor.Add("j", Jenkins);
        }

        public Command IncomingToArgs(Incoming incoming)
        {
            var raw = incoming.text.Substring(incoming.trigger_word.Length + 1).Trim();
            var spaceIndex = raw.IndexOf(' ');
            var command = spaceIndex > 0 ? raw.Substring(0, spaceIndex) : raw;
            return new Command()
            {
                Channel = incoming.channel_name,
                CommandMsg = command,
                RawMessage = incoming.text,
                CommandArgs = spaceIndex > 0 ? raw.Substring(command.Length + 1).Trim() : "",
                Message = raw,
                Owner = incoming.user_name,
            };
        }

        public Outgoing PassIncoming(Incoming incoming)
        {
            if (incoming.text.Length == incoming.trigger_word.Length)
                return Status(new Command() { Owner = incoming.user_name, CommandArgs = "" });
            var args = IncomingToArgs(incoming);
            if (_commandExecutor.ContainsKey(args.CommandMsg))
            {
                return _commandExecutor[args.CommandMsg](args);
            }
            return Out($"@{args.Owner} 命令不存在，请输入`!staging help`查看帮助!");
        }

    }
}
