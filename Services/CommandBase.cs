using CheckStaging.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CommandHandlerAttribute : Attribute
    {
        public readonly string[] triggers;
        
        public CommandHandlerAttribute(params string[] triggers)
        {
            this.triggers = triggers;
        }
    }

    public abstract class CommandBase<T> where T : CommandBase<T>
    {
        /// <summary>
        /// A helper func to convert created delegate to specify type
        /// </summary>
        /// <typeparam name="TDelegate"></typeparam>
        /// <param name="src"></param>
        /// <returns></returns>
        private static TDelegate CastDelegate<TDelegate>(Delegate src) where TDelegate : Delegate
        {
            if (src is TDelegate) return (TDelegate)src;
            else throw new TypeAccessException();
        }
        public static readonly Type HandlerAttrType = typeof(CommandHandlerAttribute);
        public static readonly Type HandlerType = typeof(Func<Command, Outgoing>);
        private static IEnumerable<(MethodInfo, string[])> _enumCommandHandlers()
        {
            foreach (var method in typeof(T).GetMethods())
            {
                var attrs = method.GetCustomAttributes(HandlerAttrType, false);
                if (attrs.Length == 1)
                { 
                    yield return (method, ((CommandHandlerAttribute)attrs[0]).triggers);
                }
            }
        }
        private static (MethodInfo, string[])[] _allCommandHandlers = _enumCommandHandlers().ToArray();

        protected CommandBase()
        {
            foreach (var (methodInfo, alias) in _allCommandHandlers)
            {
                RegisterCommand(CastDelegate<Func<Command, Outgoing>>(methodInfo.CreateDelegate(HandlerType, this)), alias);
            }
        }

        private readonly Dictionary<string, Func<Command, Outgoing>> _commandExecutor = new Dictionary<string, Func<Command, Outgoing>>();
        protected Func<Command, Outgoing> this[string index]
        {
            get { return _commandExecutor[index]; }
        }
        protected void RegisterCommand(Func<Command, Outgoing> handler, params string[] triggers)
        {
            foreach (var trigger in triggers)
            {
                _commandExecutor.Add(trigger, handler);
            }
        }
        protected bool HasRegisterTrigger(string trigger) => _commandExecutor.ContainsKey(trigger);
        protected Command IncomingToArgs(Incoming incoming)
        {
            var raw = incoming.text.Substring(incoming.trigger_word.Length + 1).Trim();
            var spaceIndex = raw.IndexOf(' ');
            var command = spaceIndex > 0 ? raw.Substring(0, spaceIndex) : raw;
            return new Command()
            {
                Channel = incoming.channel_name,
                CommandMsg = command,
                RawMessage = incoming.text,
                CommandArgs = spaceIndex > 0 ? raw.Substring(command.Length + 1).Trim() : "",
                Message = raw,
                Owner = incoming.user_name,
            };
        }

        protected Outgoing Out(string text) => new Outgoing() { text = text };

        protected string SkipSpace(string str) => str.Trim();

    }
}
