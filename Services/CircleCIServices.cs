using CheckStaging.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    public class CircleCIServices
    {

        private static OutgoingAttachment StringToCiStatus(string status, int num, string url)
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

        public static void PassCircleCIWebhook(CircleCIWebhook webhook)
        {
            StringBuilder sb = new StringBuilder();
            List<OutgoingAttachment> attachments = new List<OutgoingAttachment>(2);
            sb.AppendLine($"**Build** [#{webhook.build_num}]({webhook.build_url}): **{webhook.status}** on branch `{webhook.branch}`");
            sb.AppendLine("---");
            string user = null;
            if (webhook.pull_requests.Length > 0)
            {
                var pr = webhook.pull_requests[0];
                var realName = ChannelService.Instance.ToBearychatName(webhook.user.vcs_type, webhook.user.login);
                if (realName != webhook.user.login) user = realName.Substring(1);
                sb.AppendLine(realName);
                attachments.Add(new OutgoingAttachment()
                {
                    title = $"{webhook.branch}({pr.head_sha.Substring(0, 6)})",
                    url = pr.url,
                    text = webhook.subject,
                });
            }
            attachments.Add(StringToCiStatus(webhook.status, webhook.build_num, webhook.build_url));
            RemindService.Instance.SendMessage(new Outgoing()
            {
                text = sb.ToString(),
                notification = "Circle CI Result",
                attachments = attachments.ToArray(),
            }, "CircleCI");
        }

    }
}
