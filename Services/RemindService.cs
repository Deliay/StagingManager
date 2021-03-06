﻿using CheckStaging.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CheckStaging.Services
{

    public struct Channel
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public struct Remind
    {
        public Channel[] Channels { get; set; }
    }

    public class RemindService
    {
        public static readonly RemindService Instance = new RemindService();
        public readonly HttpClient HttpClient = new HttpClient();
        public Remind Remind;
        public readonly Dictionary<string, Uri> PostUri;
        private readonly string ConfigurationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remind.json");
        private RemindService()
        {
            Remind = (Remind)JsonConvert.DeserializeObject(File.ReadAllText(ConfigurationPath), typeof(Remind));
            if (Remind.Channels.Length == 0)
            {
                Console.WriteLine("提醒服务不可用");
                return;
            }
            PostUri = Remind.Channels.ToDictionary(key => key.Name, value => new Uri(value.Url));
        }
        public bool HasChannel(string channel)
        {
            return PostUri.ContainsKey(channel);
        }
        private static DateTime GetNextNotifyTime()
        {
            // after 18:00, notify in tomorrow 10:00AM
            if (DateTime.Now.Hour >= 18)
            {
                return DateTime.Today.AddDays(1).AddHours(10);
            }
            // after 18:00, notify in tomorrow 10:00AM
            else if (DateTime.Now.Hour >= 14)
            {
                return DateTime.Today.AddHours(18);
            }
            // after 12:00AM, notify in 18:00
            else if (DateTime.Now.Hour >= 12)
            {
                return DateTime.Today.AddHours(14);
            }
            // after 10:00AM, notify in 12:00AM
            else if (DateTime.Now.Hour >= 10)
            {
                return DateTime.Today.AddHours(12);
            }
            else if (DateTime.Now.Hour < 10)
            {
                return DateTime.Today.AddHours(10);
            }
            else
            {
                Console.WriteLine($"Uncatch time {DateTime.Now}");
                return DateTime.Today.AddDays(1).AddHours(10);
            }
        }

        private readonly Action<Timer, Action> Notify = (t, a) =>
        {
            var nextTime = GetNextNotifyTime();

            var nextSpan = nextTime - DateTime.Now;
            Console.WriteLine($"Next notify in {nextTime}, ({nextSpan.TotalHours} hour(s) left.)");
            a();
            t.Change(nextSpan, TimeSpan.Zero);
        };
        private readonly Action<Timer, Action> Boardcast = (t, a) =>
        {
            var nextTime = DateTime.Today.AddDays(1);

            var nextSpan = nextTime - DateTime.Now;
            Console.WriteLine($"Next Boardcast in {nextTime}, ({nextSpan.TotalHours} hour(s) left.)");
            a();
            t.Change(nextSpan, TimeSpan.Zero);
        };
        private readonly Action<Timer, Action> Fivithsec = (t, a) =>
        {
            var nextTime = DateTime.Now.AddSeconds(15);
            var nextSpan = nextTime - DateTime.Now;
            Console.WriteLine($"Next fetch jenkins build status in {nextTime}, {nextSpan.TotalSeconds} second(s) left");
            a();
            t.Change(nextSpan, TimeSpan.Zero);
        };

        /// <summary>
        /// Send message to channel
        /// </summary>
        /// <param name="msg">the message should to send</param>
        /// <param name="channel">empty for fist channel in configuration</param>
        public void SendMessage(string msg, string channel = "")
        {
            SendMessage(new Outgoing() { text = msg }, channel);
        }
        /// <summary>
        /// Send message to channel
        /// </summary>
        /// <param name="msg">the message want to send</param>
        /// <param name="channel">the channel want to specify</param>
        public void SendMessage(Outgoing msg, string channel = "")
        {
            var realChannel = channel == "" || !PostUri.ContainsKey(channel) ? PostUri.First().Value : PostUri[channel];
            using (var res = HttpClient.PostAsJsonAsync(realChannel, msg).Result)
            {
                if (res.IsSuccessStatusCode)
                    Console.WriteLine($"Send message: {msg}");
                else
                    Console.WriteLine($"Bearychat FAIL! Request {realChannel} get {res.StatusCode.ToString()}");
            }
        }
        public void ScheduleTask(AllStaging ss)
        {
            Action scheduleWillExpired = () =>
            {
                StringBuilder sb = new StringBuilder();
                foreach (var s in ss.Stagings)
                {
                    if (!StagingService.Instance.IsStagingInUse(s)) continue;
                    var expireTime = (s.StartTime.AddDays(s.Timeleft) - DateTime.Today).TotalDays;
                    if (expireTime < 2)
                    {
                        Console.WriteLine($"Staging{s.StagingId} will expired in today, please renew it or prepare to release.");
                        sb.AppendLine($"@{s.Owner} 你占用的Staging{s.StagingId} 今天即将过期，请注意续期或者释放！");
                    }
                }
                Instance.SendMessage(sb.ToString());
            };
            Action scheduleIsAlreadyExpired = () =>
            {
                foreach (var s in ss.Stagings)
                {
                    if (!StagingService.Instance.IsStagingInUse(s) && s.StartTime.AddDays(s.Timeleft) == DateTime.Today)
                    {
                        Console.WriteLine($"Attention please, Staging{s.StagingId} is already expired.");
                        SendMessage($"@{s.Owner} 你占用且并未释放的Staging{s.StagingId} 已经过期。");
                    }
                }
                var nextQueue = StagingService.Instance.ProceedQueueTask();
                while (nextQueue.Item1 != null)
                {
                    var s = nextQueue.Item2;
                    SendMessage($"@{s.Owner} 系统自动为你分配了Staging{s.StagingId}。");
                    nextQueue = StagingService.Instance.ProceedQueueTask();
                }
            };
            Action scheduleJenkinsBuildRefresh = () =>
            {
                try
                {
                    if (JenkinsServices.Instance.JenkinsStatus)
                    {
                        JenkinsServices.Instance.GetPipeline();
                        JenkinsServices.Instance.PeekWhileNotInBuild();
                    }
                }
                catch (Exception e)
                {
                    JenkinsServices.Instance.OnError(e);
                }
            };
            Console.WriteLine($"scheduleWillExpired will execute at {GetNextNotifyTime()}  ({(GetNextNotifyTime() - DateTime.Now).TotalHours} hour(s) left.)");
            ScheduleTaskServices.Instance.RegisterScheduleTask(scheduleWillExpired, GetNextNotifyTime() - DateTime.Now, TimeSpan.Zero, Notify);
            Console.WriteLine($"scheduleIsAlreadyExpired will execute at {DateTime.Today.AddDays(1)}  ({(DateTime.Today.AddDays(1) - DateTime.Now).TotalHours} hour(s) left.)");
            ScheduleTaskServices.Instance.RegisterScheduleTask(scheduleIsAlreadyExpired, DateTime.Today.AddDays(1) - DateTime.Now, TimeSpan.Zero, Boardcast);
            if (JenkinsServices.Instance.JenkinsStatus)
            {
                ScheduleTaskServices.Instance.RegisterScheduleTask(scheduleJenkinsBuildRefresh, DateTime.Now.AddSeconds(15) - DateTime.Now, TimeSpan.Zero, Fivithsec);
            }
        }
    }
}
