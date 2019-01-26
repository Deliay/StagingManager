using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    public class ScheduleTaskServices
    {
        public static readonly ScheduleTaskServices Instance = new ScheduleTaskServices();
        private ScheduleTaskServices() { }

        private LinkedList<Timer> _timers = new LinkedList<Timer>();

        public Timer RegisterScheduleTask<T>(T task, TimeSpan firstDelay, TimeSpan nextDelay, Action<Timer, T> onExecute)
            where T : Delegate
        {

            lock (_timers)
            {
                var node = _timers.AddLast(new Timer((t) => onExecute(t as Timer, task)));
                node.Value.Change(firstDelay, nextDelay);
                return node.Value;
            }
        }

        public bool UnregisterScheduleTask(Timer timer)
        {
            using(timer)
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                lock (_timers)
                {
                    return _timers.Remove(timer);
                }
            }
            
        }
    }
}
