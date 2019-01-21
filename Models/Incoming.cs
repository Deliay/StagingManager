using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CheckStaging.Models
{
    public struct Incoming
    {
        public string token { get; set; }
        public long ts { get; set; }
        public string text { get; set; }
        public string trigger_word { get; set; }
        public string subdomain { get; set; }
        public string channel_name { get; set; }
        public string user_name { get; set; }
    }
}
