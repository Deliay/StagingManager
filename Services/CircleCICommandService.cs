using CheckStaging.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    public struct AccountBind
    {
        public Dictionary<string, Dictionary<string ,string>> bind { get; set; }
    }

    public class CircleCICommandService : CommandBase<CircleCICommandService>
    {
        public static readonly CircleCICommandService Instance = new CircleCICommandService();
        private readonly string ConfigurationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "github.json");
        public AccountBind AccountBind;
        private CircleCICommandService()
        {
            if (File.Exists(ConfigurationPath))
            {
                lock (ConfigurationPath)
                {
                    try
                    {
                        AccountBind = (AccountBind)JsonConvert.DeserializeObject(File.ReadAllText(ConfigurationPath), typeof(AccountBind));
                    }
                    catch (Exception)
                    {
                        AccountBind = new AccountBind();
                    }
                }
            }
            if (AccountBind.bind == null) AccountBind.bind = new Dictionary<string, Dictionary<string, string>>();
            Console.WriteLine($"载入了{AccountBind.bind.Count}个渠道");
        }

        [CommandHandler("bind", "b")]
        public Outgoing Bind(Command command)
        {
            var indexSpace = command.CommandArgs.IndexOf(" ");
            var channel = command.CommandArgs.Substring(0, indexSpace);
            var account = command.CommandArgs.Substring(indexSpace + 1);
            if (!AccountBind.bind.ContainsKey(channel))
            {
                AccountBind.bind.Add(channel, new Dictionary<string, string>());
            }
            if (AccountBind.bind[channel].TryGetValue(account, out var binder))
            {
                if (binder != command.Owner)
                    return new Outgoing() { text = $"@{command.Owner} 你想绑定的{channel}的{account}已经被 @{binder} 绑定了。" };
                else
                    return new Outgoing() { text = $"@{command.Owner} 你已经绑定了这个账号" };
            }
            if (AccountBind.bind[channel].ContainsValue(command.Owner))
            {
                foreach (var pair in AccountBind.bind[channel])
                {
                    if (pair.Value == command.Owner)
                        return new Outgoing() { text = $"@{command.Owner} 你已经绑定{pair.Key}了，不能再绑定了。" };
                }
            }
            lock (ConfigurationPath)
            {
                AccountBind.bind[channel].Add(account, command.Owner);
                _save();
            }
            return new Outgoing() { text = $"@{command.Owner} 你成功绑定了{channel}的{account}。" };
        }

        [CommandHandler("unbind", "u")]
        public Outgoing Unbind(Command command)
        {
            var channel = command.CommandArgs;

            if (AccountBind.bind.TryGetValue(channel, out var value))
            {
                if (value.ContainsValue(command.Owner))
                foreach (var pair in value)
                    if (pair.Value == command.Owner)
                    {
                        lock (ConfigurationPath)
                        {
                            value.Remove(pair.Key);
                            _save();
                        }
                        return new Outgoing() { text = $"@{command.Owner} 你成功解绑了`{channel}`的`{pair.Key}`" };
                    }
            }
            return new Outgoing() { text = $"@{command.Owner} 你的操作没有解绑任何账号" };
        }

        public Outgoing CommandHelp(string owner)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("这里是ci通知机器人，ci状态的变化，我会at并通知你详情哦~");
            sb.AppendLine("---");
            sb.AppendLine("命令列表");
            sb.AppendLine("`!ci bind (渠道) (账号)` 绑定账号");
            sb.AppendLine("`!ci unbind (渠道)` 解绑账号");
            sb.AppendLine("---");
            sb.AppendLine("`!ci bind github longxuan123` 绑定github的longxuan123");
            sb.AppendLine("---");
            bool isBind = false;
            foreach (var channel in AccountBind.bind)
            {
                foreach (var pair in channel.Value)
                {
                    if (pair.Value == owner)
                    {
                        if (isBind == false)
                        {
                            sb.AppendLine($"@{owner} 你目前绑定了:");
                            isBind = true;
                        }

                        sb.AppendLine($"1. `{channel.Key}`的`{pair.Key}`");
                    }
                }
            }
            if (isBind == false)
            {
                sb.AppendLine($"@{owner} 你还没绑定账号。");
            }
            sb.AppendLine("---");
            sb.AppendLine("小提示");
            sb.AppendLine("github账号的话点击你的github头像，看URL里那个就是~");
            return new Outgoing() { text = sb.ToString() };
        }

        public Outgoing PassIncoming(Incoming incoming)
        {
            if (incoming.text.Length == incoming.trigger_word.Length)
                return CommandHelp(incoming.user_name);
            var args = IncomingToArgs(incoming);
            if (base.HasRegisterTrigger(args.CommandMsg))
            {
                return base[args.CommandMsg](args);
            }
            return new Outgoing() { text = $"@{args.Owner} 命令不存在，请输入`!ci`查看帮助!" };

        }

        private string GetNotifyName(string channel, string name)
        {
            // exist this channel
            if (AccountBind.bind.ContainsKey(channel))
            {
                // exist bind
                if (AccountBind.bind[channel].ContainsKey(name))
                {
                    return $"@{AccountBind.bind[channel][name]}({name})";
                }
            }
            return name;
        }

        private OutgoingAttachment StringToCiStatus(string status, int num, string url)
        {
            var friendlyStatus = "挂";
            var color = "#fe2e2e";
            switch (status)
            {
                case "success":
                case "fixed":
                    friendlyStatus = "过";
                    color = "#81F781";
                    break;
                case "failed":
                    friendlyStatus = "挂";
                    break;
            }
            return new OutgoingAttachment()
            {
                title = $"你的ci #{num}",
                text = $"**{friendlyStatus}**了",
                color = color,
                url = url
            };
        }

        public void PassCircleCIWebhook(CircleCIWebhook webhook)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"**Build** [#{webhook.build_num}]({webhook.build_url}): **{webhook.status}** on branch `{webhook.branch}`");
            sb.AppendLine("---");
            string user = null;
            if (webhook.pull_requests.Length > 0)
            {
                var pr = webhook.pull_requests[0];
                var realName = GetNotifyName(webhook.user.vcs_type, webhook.user.login);
                if (realName != webhook.user.login) user = realName.Substring(1);
                sb.AppendLine($"Pull Request: [{pr.head_sha.Substring(0, 6)}]({pr.url}) - {realName}");
                sb.AppendLine($"> {webhook.subject}");
            }
            RemindService.Instance.SendMessage(new Outgoing()
            {
                text = sb.ToString(),
                notification = "Circle CI Result",
                attachments = new OutgoingAttachment[]
                {
                    StringToCiStatus(webhook.status, webhook.build_num, webhook.build_url)
                },
                user = user,
            }, "CircleCI");
        }

        private void _save()
        {
            lock (ConfigurationPath)
            {
                File.WriteAllText(ConfigurationPath, JsonConvert.SerializeObject(AccountBind));
            }
        }
    }
}
