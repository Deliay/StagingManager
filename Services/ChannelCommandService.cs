using CheckStaging.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    public class ChannelCommandService : CommandBase<ChannelCommandService>
    {
        public static readonly ChannelCommandService Instance = new ChannelCommandService();
        private ChannelCommandService()
        {
        }

        [CommandHandler("bind", "b")]
        public Outgoing Bind(Command command)
        {
            var indexSpace = command.CommandArgs.IndexOf(" ");
            var channel = command.CommandArgs.Substring(0, indexSpace);
            var account = command.CommandArgs.Substring(indexSpace + 1);
            return new Outgoing() { text = ChannelService.Instance.BindChannel(command.Owner, channel, account) };
        }

        [CommandHandler("unbind", "u")]
        public Outgoing Unbind(Command command)
        {
            var channel = command.CommandArgs;
            return new Outgoing() { text = ChannelService.Instance.UnbindChannel(command.Owner, channel) };
        }

        public Outgoing CommandHelp(string owner)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("这里是渠道绑定小助手");
            sb.AppendLine("---");
            sb.AppendLine("命令列表");
            sb.AppendLine("`!ch bind (渠道) (账号)` 绑定账号");
            sb.AppendLine("`!ch unbind (渠道)` 解绑账号");
            sb.AppendLine("---");
            sb.AppendLine("`!ch bind github longxuan123` 绑定github的longxuan123");
            sb.AppendLine("---");
            bool isBind = false;
            foreach (var channel in ChannelService.Instance.AllBinding)
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
            return new Outgoing() { text = $"@{args.Owner} 命令不存在，请输入`!ch`查看帮助!" };

        }

    }
}
