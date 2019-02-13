namespace CheckStaging.Models
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
}
