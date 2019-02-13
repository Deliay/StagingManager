using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CheckStaging.Models
{
    public struct CirclePR
    {
        public string head_sha { get; set; }
        public string url { get; set; }
    }

    public struct CircleCIUser
    {
        public string login { get; set; }
        public string avatar_url { get; set; }
        public string name { get; set; }
        public int id { get; set; }
        public string vcs_type { get; set; }
    }
    public struct CircleCIWebhook
    {
        public string branch { get; set; }
        public CircleCIUser user { get; set; }
        public string status { get; set; }
        public string subject { get; set; }
        public CirclePR[] pull_requests { get; set; }
        public string outcome { get; set; }
        public int build_num { get; set; }
        public string build_url { get; set; }
    }
    public struct CircleCIWebhookPayload
    {
        public CircleCIWebhook payload { get; set; }
    }

}
