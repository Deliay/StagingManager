using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    [Serializable]
    public class Staging
    {
        public string Owner { get; set; }
        public DateTime StartTime { get; set; }
        public int Timeleft { get; set; }
        public int StagingId { get; set; }
        [NonSerialized]
        public List<string> ListPartners = new List<string>();
        public string[] Partners { get => ListPartners.ToArray(); set => ListPartners = new List<string>(value); }

        public bool IsSpecialStaging()
        {
            return StagingService.SpecialStagingOwner.Contains(this.Owner);
        }

        public Staging(int sid)
        {
            StagingId = sid;
            Owner = string.Empty;
            Timeleft = 0;
            StartTime = DateTime.Today;
            Partners = new string[] { };
        }
    }

    [Serializable]
    public class QueueTask
    {
        public string Owner { get; set; }
        public int Timeleft { get; set; }
        public int[] PreferStaging { get; set; }
    }

    [Serializable]
    public class AllStaging
    {
        [NonSerialized]
        public Queue<QueueTask> QueueTasks = new Queue<QueueTask>();
        public Staging[] Stagings { get; set; }
        public QueueTask[] Tasks { get => QueueTasks.ToArray(); set => QueueTasks = new Queue<QueueTask>(value); }
        public AllStaging()
        {
            Stagings = Enumerable.Range(1, StagingService.MAX_STAGING_COUNT).Select(i => new Staging(i)).ToArray();
            Stagings[2].Owner = "对外小联调";
            Stagings[2].Timeleft = 99999;
            Stagings[8].Owner = "仿真环境";
            Stagings[8].Timeleft = 99999;
            Stagings[14].Owner = "自动化测试";
            Stagings[14].Timeleft = 99999;
        }
    }

    public class StagingService
    {
        public const int MAX_STAGING_COUNT = 19;
        public const string INTEGRATION_OWNER = "集成测试";
        public static readonly string[] SpecialStagingOwner = new string[] { "对外小联调", "仿真环境", "自动化测试", INTEGRATION_OWNER };
        public AllStaging AllStaging;
        public static readonly StagingService Instance = new StagingService();
        private readonly string ConfigurationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configuration.json");
        private StagingService()
        {
            if (File.Exists(ConfigurationPath))
            {
                lock (ConfigurationPath)
                {
                    try
                    {
                        AllStaging = (AllStaging)JsonConvert.DeserializeObject(File.ReadAllText(ConfigurationPath), typeof(AllStaging));
                        Console.WriteLine($"配置文件加载成功 {AllStaging.QueueTasks.Count} QueueTask");
                    }
                    catch (Exception)
                    {
                        AllStaging = new AllStaging();
                    }
                    RemindService.Instance.ScheduleTask(AllStaging);
                }
            }
            else
            {
                AllStaging = new AllStaging();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Staging[] GetAllStaging() => AllStaging.Stagings;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Staging GetStaging(int n) => n > 0 ? AllStaging.Stagings[n - 1] : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStagingInUse(int n) => IsStagingInUse(GetStaging(n));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStagingInUse(Staging staging)
        {
            if (staging.StartTime.AddDays(staging.Timeleft) > DateTime.Today) return true;
            else return false;
        }

        public IEnumerable<Staging> NonIdleStagings() => Instance.AllStaging.Stagings.Where(IsStagingInUse);

        public IEnumerable<Staging> IdleStagings() => Instance.AllStaging.Stagings.Where(c => !IsStagingInUse(c));

        public int IdleStagingCount() => IdleStagings().Count();

        public bool Integration()
        {
            var staging = GetStaging(2);
            if (staging.Owner == INTEGRATION_OWNER)
            {
                staging.Owner = string.Empty;
                staging.Timeleft = 0;
                staging.StartTime = DateTime.Today;
                staging.Partners = new string[] { };
                return true;
            }
            else
            {
                staging.Owner = INTEGRATION_OWNER;
                staging.Timeleft = 1;
                staging.StartTime = DateTime.Today;
                staging.Partners = new string[] { };
                return false;
            }
        }

        public (Staging, QueueTask, string) CaptureStaging(string Owner, int Time, params int[] perfer)
        {
            if (Time >= 365 || Time <= 0)
            {
                return (null, null, "时间只能在(0, 365]之间");
            }
            Staging staging = null;
            if (GetAllStaging().Any(s => s.Owner == Owner && perfer.Contains(s.StagingId) && IsStagingInUse(s.StagingId)))
            {
                return (null, null, "你已经占了期望的Staging了，请换个Staging");
            }
            if (perfer.Length == 0 && GetAllStaging().Any(s => s.Owner == Owner && IsStagingInUse(s.StagingId)))
            {
                return (null, null, "你已经占了任意一台Staging，如需其他Staging，请指定Staging");
            }

            if (perfer.Length > 0)
            {
                staging = GetStaging(perfer.FirstOrDefault(id => !IsStagingInUse(id)));
            }
            else
            {
                staging = GetAllStaging().FirstOrDefault(s => !IsStagingInUse(s.StagingId));
            }

            if (staging != null)
            {
                staging.Owner = Owner;
                staging.StartTime = DateTime.Today;
                staging.Timeleft = Time;
                staging.Partners = new string[] { };
                _save();
                return (staging, null, string.Empty);
            }

            var task = new QueueTask() { Owner = Owner, PreferStaging = perfer, Timeleft = Time };
            AllStaging.QueueTasks.Enqueue(task);
            _save();
            return (null, task, string.Empty);
        }

        public bool RenewStaging(string Owner, int stagingId)
        {
            if (IsStagingInUse(stagingId) && GetStaging(stagingId).Owner == Owner)
            {
                GetStaging(stagingId).Timeleft++;
                _save();
                return true;
            }
            return false;
        }

        public void CancelAllTask(string Owner)
        {
            if (AllStaging.QueueTasks.Any(t => t.Owner == Owner))
                lock (AllStaging)
                {
                    AllStaging.Tasks = AllStaging.QueueTasks.Where(t => t.Owner != Owner).ToArray();
                    _save();
                }
        }

        public (QueueTask, Staging, IEnumerable<Staging>) ReleaseStaging(string Owner, params int[] stagingIds)
        {
            lock (AllStaging)
            {
                IEnumerable<Staging> realStagings = null;
                if (stagingIds.Length == 0)
                {
                    realStagings = GetAllStaging().Where(s => s.Owner == Owner);
                }
                else
                {
                    realStagings = stagingIds.Where(sid => GetStaging(sid).Owner == Owner).Select(GetStaging);
                }
                LinkedList<Staging> removedStaging = new LinkedList<Staging>();
                foreach (var staging in realStagings)
                {
                    if (staging.Owner == Owner)
                    {
                        staging.Owner = string.Empty;
                        staging.StartTime = DateTime.Today;
                        staging.Timeleft = 0;
                        removedStaging.AddLast(staging);
                    }
                }
                if (removedStaging.Count > 0)
                {
                    var (queue, task) = ProceedQueueTask();
                    return (queue, task, removedStaging);
                }
                _save();
                return (null, null, removedStaging);
            }
        }

        public (QueueTask, Staging) ProceedQueueTask()
        {
            var first = AllStaging.QueueTasks.FirstOrDefault();
            var last = AllStaging.QueueTasks.LastOrDefault();
            while (AllStaging.QueueTasks.TryPeek(out QueueTask task))
            {
                // 如果没有任意staging，则跳过本次处理
                if (IdleStagingCount() == 0)
                {
                    return (null, null);
                }

                // 如果规定了staging，但没有找到合适的staging，则重新入队伍
                if (task.PreferStaging.Length != 0 && IdleStagings().All(s => !task.PreferStaging.Contains(s.StagingId)))
                {
                    AllStaging.QueueTasks.Enqueue(AllStaging.QueueTasks.Dequeue());
                    // 如果是最后一个队列任务，也跳出本次计算
                    if (task == last)
                    {
                        return (null, null);
                    }
                    continue;
                }

                var staging = task.PreferStaging.Length != 0 
                    ? GetAllStaging().First(s => !IsStagingInUse(s) && task.PreferStaging.Contains(s.StagingId)) 
                    : IdleStagings().First();
                staging.Owner = task.Owner;
                staging.StartTime = DateTime.Today;
                staging.Timeleft = task.Timeleft;
                AllStaging.QueueTasks.Dequeue();
                // 如果找到了staging，则将所有剩余的请求重新入队
                var currLast = AllStaging.QueueTasks.LastOrDefault();
                while (AllStaging.QueueTasks.TryPeek(out QueueTask next) && currLast != next)
                {
                    AllStaging.QueueTasks.Enqueue(AllStaging.QueueTasks.Dequeue());
                }
                if (first != last)
                {
                    AllStaging.QueueTasks.Enqueue(AllStaging.QueueTasks.Dequeue());
                }
                _save();
                return (task, staging);
            }
            return (null, null);
        }

        public (bool, bool) Together(string Owner, int stagingId, string partner)
        {
            var isSpecial = GetStaging(stagingId).IsSpecialStaging();
            if (!isSpecial && (!IsStagingInUse(stagingId) || !(GetStaging(stagingId).Owner == Owner)))
            {
                return (false, false);
            }
            if (isSpecial && Owner != partner)
            {
                return (false, false);
            }
            var isAdd = true;
            if (!GetStaging(stagingId).ListPartners.Contains(partner))
            {
                GetStaging(stagingId).ListPartners.Add(partner);
            }
            else
            {
                GetStaging(stagingId).ListPartners.Remove(partner);
                isAdd = false;
            }
            _save();
            return (true, isAdd);
        }

        private void _save()
        {
            lock (ConfigurationPath)
            {
                File.WriteAllText(ConfigurationPath, JsonConvert.SerializeObject(AllStaging));
            }
        }
    }
}
