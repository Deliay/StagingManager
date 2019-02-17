using CheckStaging.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckStaging.Services
{
    public struct AccountBind
    {
        public Dictionary<string, Dictionary<string ,string>> bind { get; set; }
    }

    public class ChannelService
    {
        public static readonly ChannelService Instance = new ChannelService();
        private readonly string ConfigurationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "github.json");
        private AccountBind AccountBind;
        public IReadOnlyDictionary<string, Dictionary<string, string>> AllBinding { get => AccountBind.bind; }
        private ChannelService()
        {
            if (File.Exists(ConfigurationPath))
            {
                lock (ConfigurationPath)
                {
                    try
                    {
                        AccountBind = (AccountBind)JsonConvert.DeserializeObject(File.ReadAllText(ConfigurationPath), typeof(AccountBind));
                    }
                    catch (Exception)
                    {
                        AccountBind = new AccountBind();
                    }
                }
            }
            if (AccountBind.bind == null) AccountBind.bind = new Dictionary<string, Dictionary<string, string>>();
            Console.WriteLine($"载入了{AccountBind.bind.Count}个渠道");
        }

        public string ToBearychatName(string channel, string name)
        {
            // exist this channel
            if (AccountBind.bind.ContainsKey(channel))
            {
                // exist bind
                if (AccountBind.bind[channel].ContainsKey(name))
                {
                    return $"@{AccountBind.bind[channel][name]} ({name})";
                }
            }
            return name;
        }

        public string BindChannel(string owner, string channel, string account)
        {
            if (!AccountBind.bind.ContainsKey(channel))
            {
                AccountBind.bind.Add(channel, new Dictionary<string, string>());
            }
            if (AccountBind.bind[channel].TryGetValue(account, out var binder))
            {
                if (binder != owner)
                    return $"@{owner} 你想绑定的{channel}的{account}已经被 @{binder} 绑定了。";
                else
                    return $"@{owner} 你已经绑定了这个账号";
            }
            if (AccountBind.bind[channel].ContainsValue(owner))
            {
                foreach (var pair in AccountBind.bind[channel])
                {
                    if (pair.Value == owner)
                        return $"@{owner} 你已经绑定{pair.Key}了，不能再绑定了。";
                }
            }
            lock (ConfigurationPath)
            {
                AccountBind.bind[channel].Add(account, owner);
                _save();
            }
            return $"@{owner} 你成功绑定了{channel}的{account}。";
        }

        public string UnbindChannel(string owner, string channel)
        {

            if (AccountBind.bind.TryGetValue(channel, out var value))
            {
                if (value.ContainsValue(owner))
                    foreach (var pair in value)
                        if (pair.Value == owner)
                        {
                            lock (ConfigurationPath)
                            {
                                value.Remove(pair.Key);
                                _save();
                            }
                            return $"@{owner} 你成功解绑了`{channel}`的`{pair.Key}`";
                        }
            }
            return $"@{owner} 你的操作没有解绑任何账号";
        }

        private void _save()
        {
            lock (ConfigurationPath)
            {
                File.WriteAllText(ConfigurationPath, JsonConvert.SerializeObject(AccountBind));
            }
        }
    }
}
