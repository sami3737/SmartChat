using System;
using System.Collections.Generic;
using System.Globalization;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Random = System.Random;
using Time = Oxide.Core.Libraries.Time;

namespace Oxide.Plugins
{
    [Info("Smart Chat", "sami37", "1.0.0")]
    public class SmartChat : RustPlugin
    {
        private Random rand = new Random();
        private readonly Dictionary<BasePlayer, uint> _lastSent = new Dictionary<BasePlayer, uint>();
        private uint _lastSentGlobal;
        bool betterchat = false;
        Configuration config;
        CultureInfo culture = CultureInfo.CurrentCulture;
        private static readonly Time Time = GetLibrary<Time>();

        class Configuration
        {
            [JsonProperty(PropertyName = "Trigger List")]
            public Settings Info = new Settings();

            public class Settings
            {
                [JsonProperty(PropertyName = "Trigger", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<trigger> TriggerSetup = new List<trigger>
                {
                    new trigger
                    {
                        TriggerWords = new List<string>()
                        {
                            "when is wipe",
                            "when wipe",
                            "map wipe",
                            "wipe"
                        },
                        Response = new List<string>()
                        {
                            "Wipes are every day on 12:00",
                            "Wipes every 12:00",
                            "Wipes on low pop"
                        },
                        ResponseDelay = 0,
                        GlobalCD = 300,
                        Log = false,
                        Public = false,
                        SteamID = 103582791465278199
                    },
                    new trigger
                    {
                        TriggerWords = new List<string>()
                        {
                            "Blabla"
                        },
                        Response = new List<string>()
                        {
                            "Stop blabla",
                            "Well blabla",
                            "Bla Bla Bla...."
                        },
                        ResponseDelay = 0,
                        GlobalCD = 300,
                        Log = false,
                        Public = false,
                        SteamID = 103582791465278199
                    }
                };

                public class trigger
                {
                    [JsonProperty(PropertyName = "Trigger Words")]
                    public List<string> TriggerWords;

                    [JsonProperty(PropertyName = "Response")]
                    public List<string> Response;

                    [JsonProperty(PropertyName = "Response delay")]
                    public int ResponseDelay;

                    [JsonProperty(PropertyName = "Sender steamID")]
                    public ulong SteamID;

                    [JsonProperty(PropertyName = "Public response")]
                    public bool Public;

                    [JsonProperty(PropertyName = "Global cooldown")]
                    public int GlobalCD;

                    [JsonProperty(PropertyName = "Log usage")]
                    public bool Log;
                }

                [JsonProperty(PropertyName = "Delay between command")]
                public int DelayCommand = 1;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        private void OnServerInitialized()
        {
            LoadConfig();

            var pluginList = plugins.GetAll();
            foreach (var plug in pluginList)
            {
                if (plug.Name == "BetterChat" || plug.Name == "Better Chat")
                {
                    betterchat = true;
                    PrintWarning("Detected Better Chat. We will use it.");
                }
            }
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            object playerObj, messageObj, chatChannel;
            if (!data.TryGetValue("Player", out playerObj) || !data.TryGetValue("Message", out messageObj) ||
                !data.TryGetValue("ChatChannel", out chatChannel))
            {
                return null;
            }

            if ((Chat.ChatChannel) chatChannel != Chat.ChatChannel.Global)
            {
                return null;
            }

            var player = BasePlayer.Find((playerObj as IPlayer)?.Id);
            var message = messageObj?.ToString();
            if (player == null || !player.IsConnected || string.IsNullOrEmpty(message))
                return null;

            if (HandleChatMessage(player, message) == null)
                return null;

            data["CancelOption"] = 2;
            return null;
        }

        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (betterchat)
                return null;
            if (player == null || string.IsNullOrEmpty(message) || channel != Chat.ChatChannel.Global)
                return null;

            return HandleChatMessage(player, message);
        }

        public object HandleChatMessage(BasePlayer player, string msg)
        {
            var tNow = Time.GetUnixTimestamp();
            uint lastSent;
            bool matched = false;
            _lastSent.TryGetValue(player, out lastSent);
            foreach (var message in config.Info.TriggerSetup)
            {
                foreach (var messageTriggerWord in message.TriggerWords)
                {
                    if (culture.CompareInfo.IndexOf(msg, messageTriggerWord, CompareOptions.IgnoreCase) >= 0)
                    {
                        var random = rand.Next(0, message.Response.Count - 1);
                        if (message.ResponseDelay != 0 && lastSent + message.ResponseDelay > tNow ||
                            message.GlobalCD != 0 && message.Public && _lastSentGlobal + message.GlobalCD > tNow)
                            return null;
                        timer.Once(message.ResponseDelay, () => TrySend(player, message.Public, message.Response[random], message.SteamID));
                        matched = true;
                        
                    }
                    if (matched)
                        break;
                }
            }

            if (matched)
            {
                _lastSent[player] = tNow;
                _lastSentGlobal = tNow;
            }

            return null;
        }

        private void TrySend(BasePlayer player, bool isPublic, string answer, ulong steamid)
        {
            if (isPublic)
                Publish(answer, true, steamid);
            else
                Publish(player, answer, steamid);
        }

        private void Publish(string message, bool exclude = true, ulong steamid = 0UL)
        {
            if (!exclude) return;
            var players = BasePlayer.activePlayerList;
            var playersCount = players.Count;
            for (var i = 0; i < playersCount; i++)
            {
                var player = players[i];
                if (player != null)
                    SendMessage(player, message, steamid);
            }
        }
        private void Publish(BasePlayer player, string message, ulong steam) => SendMessage(player, message, steam);

        private void SendMessage(BasePlayer player, string message, ulong steam)
        {
            player.SendConsoleCommand("chat.add", 2, steam, message);
        }

    }
}