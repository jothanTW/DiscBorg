using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Net.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordDSPTestConnect
{
    class ChannelInfo
    {
        public string SaveString
        {
            get { return channelID + '\t' + talkRate + '\t' + lastMessage; }
            set { string[] vals = value.Split(new char[] { '\t' }); channelID = vals[0]; lastMessage = vals[2]; try { talkRate = Convert.ToInt32(vals[1]); } catch (FormatException) { talkRate = 0; }}
        }
        public string channelID;
        public string channelName;
        public string guildName;
        public string lastMessage;
        public int talkRate;
        //public BattleshipGame bShip;
        public DSPModuleMap modules;
    }

    class Program
    {
        static DiscordClient discord;
        static Dictionary<string, ChannelInfo> channels;
        static SBorgPort borg;
        static DiceRoller roller;
        static Random rand;
        static Dictionary<string, ComObj> commands;
        static List<string> censors;
        static List<string> whitelist;
        static List<string> loadedDMs;
        static Dictionary<string, string> configs;

        static int maxMessageLook;
        static int maxMessageGet;

        static Program()
        {

            whitelist = new List<string>();

            roller = new DiceRoller("Roller");
            loadedDMs = new List<string>();

            rand = new Random();
            maxMessageLook = 100;
            maxMessageGet = 20000;
            borg = new SBorgPort();

            configs = new Dictionary<string, string>();
        }

        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                loadConfigs("configs.dat");
                loadWhitelist("whitelist.txt");
                discord = new DiscordClient(new DiscordConfiguration
                {
                    Token = configs["Bot-Token"],
                    TokenType = TokenType.Bot
                });

                commands = new Dictionary<string, ComObj>();
                commands.Add("talkrate", new ComObj("talkrate", "Set the Talkback Rate",
                    "Sets the rate (at 1/r) at which this bot responds to messages. Usage: !talkrate <number>", doTalkRate));
                commands.Add("help", new ComObj("help", "You are here.",
                    "I don't know what else to tell you.", getHelp));

                borg.loadMem("lines.txt");
                Console.Out.WriteLine("Bot Memory Loaded");

                loadChannels("ChannelInfo.tdf");
                Console.Out.WriteLine("Channel Info Loaded");

                //loadCensors("censors.dat");

                discord.SetWebSocketClient<WebSocket4NetClient>();

                discord.MessageCreated += async e =>
                {
                    try
                    {
                        if (e.Author == discord.CurrentUser)
                            return;
                        string ret = "";
                        string chanID = e.Message.ChannelId.ToString();
                        if (channels.ContainsKey(chanID)) // This should now always be true
                            channels[chanID].lastMessage = e.Message.Id.ToString();
                        string chanRec = "";
                        if (e.Channel.IsPrivate)
                        {
                            foreach (DiscordUser user in ((DiscordDmChannel)e.Channel).Recipients)
                                chanRec += user.Username + ", ";
                            Console.Out.WriteLine("Message recieved from " + chanRec.Substring(0, chanRec.Length - 2) + ": " + e.Message.Content);
                        }
                        else
                            Console.Out.WriteLine("Message recieved from channel (" + e.Guild.Name + ")" + e.Channel.Name + ": " + e.Message.Content);

                        bool isAdmin = whitelist.Contains(e.Author.Id.ToString());
                        if (e.Message.Content[0] == DSPModuleMap.MOD_ID)
                        {
                            ret = channels[chanID].modules.command(e.Message.Content.ToLower(), isAdmin);
                        }
                        else if (e.Message.Content[0] == '!')
                        {
                            string[] com = e.Message.Content.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

                            if (commands.ContainsKey(com[0].Substring(1).ToLower()))
                            {
                                string cargs = chanID;
                                if (com.Length > 1)
                                    cargs += ' ' + com[1];
                                ret = commands[com[0].Substring(1).ToLower()].doAction(cargs, isAdmin);
                            }
                        //else ret = borg.command(e.Message.Content, isAdmin);
                        else ret = channels[chanID].modules.command(e.Message.Content.ToLower(), isAdmin);
                        }
                        else if (e.MentionedUsers.Contains(discord.CurrentUser))
                        {
                            string tr = "";
                            string[] tex = e.Message.Content.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string s in tex)
                                if (!Regex.IsMatch(s, "^<@\\d>$") && s[0] != '@')
                                    tr += s + ' ';
                            ret = borg.reply(tr);
                        }
                        else
                        {
                            borg.learn(e.Message.Content);
                            if (channels.ContainsKey(chanID) && channels[chanID].talkRate > 0 && rand.Next(channels[chanID].talkRate) == 0)
                                ret = borg.reply(e.Message.Content);
                        }

                        if (ret.Trim().Length > 0)
                        {
                            if (e.Channel.IsPrivate)
                                Console.Out.WriteLine("Message sent to " + chanRec.Substring(0, chanRec.Length - 2) + ": " + ret);
                            else
                                Console.Out.WriteLine("Message sent to channel (" + e.Guild.Name + ")" + e.Channel.Name + ": " + ret);
                            await e.Message.RespondAsync(ret);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Out.WriteLine(ex.GetType().ToString() + ": " + ex.Message);
                    }
                };

                int numGuilds = 999999;

                discord.Ready += async e =>
                {
                    await Task.Yield();
                    numGuilds = discord.Guilds.Count;
                };

                discord.GuildAvailable += async e =>
                {
                    IReadOnlyList<DiscordChannel> dChannels = await e.Guild.GetChannelsAsync();
                    foreach (DiscordChannel chan in dChannels)
                    {
                        await loadLastMessages(chan);
                    }
                    numGuilds--;
                    if (numGuilds == 0)
                    {
                        borg.saveMem("lines.txt");
                        saveChannels("ChannelInfo.tdf");
                        DiscordGame game = new DiscordGame();
                        game.Name = RandomGameName.getGameName();
                        game.StreamType = GameStreamType.NoStream;
                        await discord.UpdateStatusAsync(game);
                    }
                };

                discord.DmChannelCreated += async e =>
                {
                    if (loadedDMs.Contains(e.Channel.Id.ToString()))
                        return;
                    await loadLastMessages(e.Channel);
                    loadedDMs.Add(e.Channel.Id.ToString());
                };

                await discord.ConnectAsync();

                CancellationTokenSource exitSource = new CancellationTokenSource();
                CancellationToken exitToken = exitSource.Token;
                try
                {
                    while (!exitSource.IsCancellationRequested)
                    {
                        await Task.Delay(1800000, exitSource.Token);
                        Console.Out.WriteLine("Backing up...");
                        borg.saveMem("lines.txt");
                        saveChannels("ChannelInfo.tdf");
                        //saveCensors("censors.dat");
                        DiscordGame game = new DiscordGame();
                        game.StreamType = GameStreamType.NoStream;
                        game.Name = RandomGameName.getGameName();
                        await discord.UpdateStatusAsync(game);
                        await Task.Delay(1800000, exitSource.Token);
                        game.Name = RandomGameName.getGameName();
                        await discord.UpdateStatusAsync(game);
                    }
                }
                catch (TaskCanceledException) { }
            } catch (Exception e) // why would this ever happen
            {
                Console.Out.WriteLine(e.GetType() + ": " + e.Message);
            }
        }

        static string getHelp(string inp)
        {
            string[] args = inp.ToLower().Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string bArgs = "";
            if (args.Length > 1)
            {
                bArgs = args[1];
                if (commands.ContainsKey(bArgs)) return commands[bArgs].Help;
                return channels[args[0]].modules.getHelp(bArgs);
            }
            string r = "Available Commands:\r\n";
            foreach (string key in commands.Keys)
                r += "\t" + key + ": " + commands[key].Desc + "\r\n";
            r += channels[args[0]].modules.getHelp(bArgs);
            return r;
        }

        static string doTalkRate(string inp)
        {
            try
            {
                string[] args = inp.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                string chanID = args[0];
                if (args.Length == 1)
                    return "Current talk rate: " + channels[chanID].talkRate;
                int tRate = 0;
                try { tRate = Convert.ToInt32(args[1]); }
                catch (OverflowException)
                {
                    tRate = (args[1][0] == '-') ? -1 : 100;
                }
                if (tRate < 0)
                {
                    return "Talk Rate can't be negative.";
                }
                if (tRate > 100)
                    tRate = 100;
                ChannelInfo info = channels[chanID];
                info.talkRate = tRate;
                channels[chanID] = info;
                return "Talk Rate set to " + tRate + ".";
            }
            catch (FormatException)
            {
                return "Usage: !talkrate <number>";
            }
        }

        static string doCensor(string inp)
        {
            string[] args = inp.Split(' ');
            string r = "";
            if (args.Length == 1)
            {
                r = "Censor List:\r\n";
                foreach (string key in censors)
                    r += key + "\r\n";
                return r;
            }
            for (int i = 1; i < args.Length; i++)
            {
                string k = args[i].ToLower();
                if (!censors.Contains(k))
                {
                    censors.Add(k);
                    r += k + " added to censor list.";
                }
                return r;
            }
            return "";
        }

        static async Task loadLastMessages(DiscordChannel chan)
        {
            if (chan.Type == ChannelType.Group || chan.Type == ChannelType.Private || chan.Type == ChannelType.Text)
            {
                string chanID = chan.Id.ToString();
                ChannelInfo cInfo;
                if (channels.ContainsKey(chanID))
                    cInfo = channels[chanID];
                else
                {
                    cInfo = new ChannelInfo();
                    cInfo.channelID = chanID;
                    cInfo.talkRate = 0;
                    channels.Add(chanID, cInfo);
                    cInfo.lastMessage = "";
                }
                if (chan.Type == ChannelType.Private)
                {
                    cInfo.guildName = "private";
                    string cName = "";
                    foreach (DiscordUser user in ((DiscordDmChannel)chan).Recipients)
                        cName += user.Username + ", ";
                    cInfo.channelName = cName.Substring(0, cName.Length - 2);
                }
                else
                {
                    cInfo.guildName = chan.Guild.Name;
                    cInfo.channelName = chan.Name;
                }
                cInfo.modules = new DSPModuleMap();
                cInfo.modules.add(borg);
                cInfo.modules.add(new BattleshipGame());
                cInfo.modules.add(roller);

                // Get messages. if we have a last message, get everything after that. if not, get up to 10000 messages before it.
                if (cInfo.lastMessage.Length > 0)
                {
                    int mRec = 0;
                    int tmRec = 0;
                    do
                    {
                        IReadOnlyList<DiscordMessage> messages = await chan.GetMessagesAsync(maxMessageLook, null, Convert.ToUInt64(cInfo.lastMessage));
                        mRec = messages.Count;
                        if (mRec == 0)
                            break;
                        tmRec += mRec;
                        cInfo.lastMessage = messages[0].Id.ToString(); // probably still get messages in reverse order
                        foreach (DiscordMessage message in messages)
                            if (message.Author != discord.CurrentUser && message.Content.Length > 0 && message.Content[0] != '!')
                                borg.learn(message.Content);
                    } while (mRec == maxMessageLook);
                    Console.Out.WriteLine(tmRec + " new messages loaded from channel (" + cInfo.guildName + ")" + cInfo.channelName);
                }
                else
                {
                    int mRec = 0;
                    int tmRec = 0;
                    ulong prevMessage = 0;
                    do
                    {
                        IReadOnlyList<DiscordMessage> messages;
                        if (prevMessage == 0)
                            messages = await chan.GetMessagesAsync(maxMessageLook);
                        else
                            messages = await chan.GetMessagesAsync(maxMessageLook, prevMessage);
                        mRec = messages.Count;
                        if (mRec == 0)
                            break;
                        tmRec += mRec;
                        if (cInfo.lastMessage.Length == 0)
                            cInfo.lastMessage = messages[0].Id.ToString();
                        prevMessage = messages[messages.Count - 1].Id; // probably still get messages in reverse order
                            foreach (DiscordMessage message in messages)
                                if (message.Author != discord.CurrentUser && message.Content.Length > 0 && message.Content[0] != '!')
                                    borg.learn(message.Content);
                    } while (mRec == maxMessageLook && tmRec < maxMessageGet);
                    Console.Out.WriteLine(tmRec + " prior messages loaded from channel (" + cInfo.guildName + ")" + cInfo.channelName);
                }
                channels[cInfo.channelID] = cInfo;
            }
        }

        static void loadChannels(string filename)
        {
            channels = new Dictionary<string, ChannelInfo>();
            try
            {
                StreamReader file = new StreamReader(filename);
                while (!file.EndOfStream)
                {
                    ChannelInfo info = new ChannelInfo();
                    info.SaveString = file.ReadLine();
                    channels.Add(info.channelID, info);
                }
                file.Close();
            }
            catch (IOException)
            {
                Console.Out.WriteLine(filename + " could not be opened; creating new");
            }
        }

        static void saveChannels(string filename)
        {
            StreamWriter file = new StreamWriter(filename, false);
            foreach (string key in channels.Keys)
                file.WriteLine(channels[key].SaveString);
            file.Close();
        }

        static string wordCensor(string line)
        {
            // assume line is already lowercase
            // censor everything in the censor list that isn't surrounded by more letters
            foreach (string key in censors)
            {
                string rep = "";
                for (int i = 0; i < key.Length; i++)
                    rep += '*';
                line = Regex.Replace(line, "\b" + key + "\b", rep);
            }
            return line;
        }

        static void saveCensors(string filename)
        {
            StreamWriter file = new StreamWriter(filename, false);
            foreach (string key in censors)
                file.WriteLine(key);
            file.Close();
        }

        static void loadCensors(string filename)
        {
            censors = new List<string>();
            try
            {
                StreamReader file = new StreamReader(filename);
                while (!file.EndOfStream)
                    censors.Add(file.ReadLine());
                file.Close();
            }
            catch (IOException)
            {
                StreamWriter file = new StreamWriter(filename);
                file.Close();
            }
        }

        static void loadWhitelist(string filename)
        {
            try
            {
                StreamReader file = new StreamReader(filename);
                while (!file.EndOfStream)
                {
                    string id = file.ReadLine();
                    if (!whitelist.Contains(id))
                        whitelist.Add(id);
                }
                file.Close();
            }
            catch (IOException)
            {
                Console.Out.WriteLine("Whitelist not found, creating empty file");
                StreamWriter file = new StreamWriter(filename);
                file.Close();
            }
        }

        static void loadConfigs(string filename)
        {
            try
            {
                StreamReader file = new StreamReader(filename);
                while (!file.EndOfStream)
                {
                    string[] vals = file.ReadLine().Split(new char[] { ':' }, 2);
                    if (vals.Length < 2)
                        continue;
                    vals[0] = vals[0].Trim();
                    vals[1] = vals[1].Trim();
                    if (configs.ContainsKey(vals[0]))
                        configs[vals[0]] = vals[1];
                    else
                        configs.Add(vals[0], vals[1]);
                }
                file.Close();
            } catch (IOException)
            {
                Console.Out.WriteLine("Error: " + filename + " config file not found");
            }
        }
    }
}
