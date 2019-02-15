using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CheckStaging.Models
{
    public struct OutgoingPicture
    {
        public string url { get; set; }
    }
    public struct OutgoingAttachment
    {
        public string title { get; set; }
        public string url { get; set; }
        public string text { get; set; }
        public string color { get; set; }
        public OutgoingPicture[] images { get; set; }
    }

    public struct Outgoing
    {
        public string text { get; set; }
        public string user { get; set; }
        public string channel { get; set; }
        public string notification { get; set; }
        public OutgoingAttachment[] attachments { get; set; }
    }
}
