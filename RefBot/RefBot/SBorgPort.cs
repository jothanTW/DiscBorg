using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordDSPTestConnect
{
    class SBorgPort : DSPModule
    {
        private struct WordLoc
        {
            int lineNum;
            int wordNum;
            public WordLoc(int l, int w)
            {
                lineNum = l;
                wordNum = w;
            }
            public int LineNum { get { return lineNum; } }
            public int WordNum { get { return wordNum; } }
        }

        private const int WOD_LEN = 4; // min length of the word of the day
        private const int WOD_EXM = 15; // min length of the word of the day example

        private static Random rand;
        private static List<string> lines;
        private static Dictionary<string, List<WordLoc>> words;
        //private Dictionary<string, ComObj> commands;
        private int minDepth;
        private int maxDepth;
        private string lastDesc;
        private DateTime lastDate;
        private string lastWOD;

        static SBorgPort()
        {
            rand = new Random();
            lines = new List<string>();
            words = new Dictionary<string, List<WordLoc>>();
        }

        public SBorgPort() : base("borg")
        {
            minDepth = 1;
            maxDepth = 4;
            lastDesc = "I haven't said anything yet...";
            lastDate = new DateTime(1970, 1, 1);

            commands = new Dictionary<string, ComObj>();

            commands.Add("context", new ComObj("contexts", "", "Get known contexts of a word. Usage: !contexts <word>", getContexts));
            commands.Add("contexts", new ComObj("contexts", "Get known contexts of a word, up to ~1000 characters",
                "Get known contexts of a word. Usage: !contexts <word>", getContexts));
            commands.Add("known", new ComObj("known", "Check if a word is known",
                "Check if a word is known. Usage: !known <word>", getKnown));
            /*commands.Add("roll", new ComObj("roll", "Roll dice, use !help roll for more info",
                "Roll dice. Usage: !roll <dice command>. Expected format: <I>#<N>d<S><f><F>, where I is the number of times to roll, N is the number of dice, "
                + "S is the dice size, f is any flag, and F is the flag argument. '<I>#', '<N>', '<N>d' are optional and implied; '<f><F>' is optional. Flags:\r\n"
                + "k: keep <F> highest rolls of NdS\r\n"
                + "l: keep <F> lowest rolls of NdS\r\n"
                + "x: reroll dice results larger than or equal to <F>, once\r\n"
                + "t: count dice results larger than or equal to <F>", doRoll));*/
            commands.Add("speak", new ComObj("speak", "Get a response; arguments optional",
                "Get a response to a phrase, or generate a random sentence. Usage: !speak <phrase?>", reply));
            commands.Add("words", new ComObj("words", "Get current learned status",
                "Get total word/line/context count. Usage: !words", getWordStatus));
            commands.Add("features", new ComObj("feature", "", "Add a feature suggestion to a log. Usage: !feature <text>", doFeature));
            commands.Add("feature", new ComObj("feature", "Suggest a feature",
                "Add a feature suggestion to a log. Usage: !feature <text>", doFeature));
            commands.Add("what", new ComObj("what", "Explain the last reply", "Get a list of the contexts used for the last reply", getLastDesc));
            commands.Add("wod", new ComObj("wod", "Word of the Day", "Get the Word of the Day for this chat", doWOD));
        }

        /*public string command(string input, bool isAdmin)
        {
            if (input[0] != '!')
                return "";
            string[] inps = input.Split(new char[] { ' ' }, 2);
            string inp = inps[0].Substring(1);
            string arg = "";
            if (inps.Length > 1)
                arg = inps[1];
            if (commands.ContainsKey(inp))
            {
                if (commands[inp].Does)
                    return commands[inp].doAction(arg, isAdmin);
                return "";
            }
            return "Command not found";
        }*/

        private static void learnSingle(string input) // for post-formatted lines
        {
            lines.Add(input);
            string[] w = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < w.Length; j++)
            {
                if (!words.ContainsKey(w[j]))
                {
                    words.Add(w[j], new List<WordLoc>());
                }
                words[w[j]].Add(new WordLoc(lines.Count - 1, j));
            }
        }

        public static string learn(string input)
        {
            string[] s = getFormattedString(input).Split(new string[] { ". ", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < s.Length; i++)
            {
                if (lines.Contains(s[i]))
                    continue;
                learnSingle(s[i]);
            }
            return "";
        }

        public string loadMem(string filename)
        {
            try
            {
                StreamReader file = new StreamReader(filename);
                while (!file.EndOfStream)
                    learnSingle(file.ReadLine());
                file.Close();
            }
            catch (IOException ex)
            {
                return ex.Message + "; creating new dictionary";
            }
            return "";
        }

        public string reply(string input)
        {
            List<string> wds = new List<string>(getFormattedString(input)
                .Replace(". ", "")
                .Split(new char[] { ' ' }));
            for (int i = 0; i < wds.Count; i++)
                if (!words.ContainsKey(wds[i]))
                    wds.RemoveAt(i--);

            if (wds.Count == 0)
                wds.Add(words.Keys.ToList()[rand.Next(words.Keys.Count)]);

            List<string> descLines = new List<string>();
            List<int> lineNums = new List<int>();
            //lastDesc = "";
            string ret = wds[rand.Next(wds.Count)];
            bool doLoop = true;
            while (doLoop) // right edge build
            {
                string thisWord = ret.Substring(ret.LastIndexOf(' ') + 1);
                WordLoc tryLoc = words[thisWord][rand.Next(words[thisWord].Count)];
                string[] wList = lines[tryLoc.LineNum].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int depth = rand.Next(maxDepth - minDepth) + minDepth;

                string descLine = "";
                for (int i = 0; i < wList.Length; i++)
                {
                    string w = wList[i].Trim('*', '_');
                    if (i == tryLoc.WordNum)
                        w = "**" + w;
                    if (i == tryLoc.WordNum + depth)
                        w += "**";
                    descLine += w;
                    if (i < wList.Length - 1)
                        descLine += ' ';
                }
                if (tryLoc.WordNum + depth >= wList.Length)
                    descLine += "**";
                //lastDesc += descLine + "\r\n";
                descLines.Add(descLine);
                lineNums.Add(tryLoc.LineNum);

                for (int i = 1; i <= depth; i++)
                {
                    if (i + tryLoc.WordNum >= wList.Length)
                    {
                        doLoop = false;
                        break;
                    }
                    ret += ' ' + wList[i + tryLoc.WordNum];
                }
            }

            doLoop = true;
            while (doLoop) // left edge build
            {
                string thisWord;
                if (ret.IndexOf(' ') == -1)
                    thisWord = ret;
                else
                    thisWord = ret.Substring(0, ret.IndexOf(' '));
                WordLoc tryLoc = words[thisWord][rand.Next(words[thisWord].Count)];
                string[] wList = lines[tryLoc.LineNum].Split(new char[] { ' ' });
                int depth = rand.Next(maxDepth - minDepth) + minDepth;

                string descLine = "";
                for (int i = 0; i < wList.Length; i++)
                {
                    string w = wList[i].Trim('*', '_');
                    if (i == tryLoc.WordNum)
                        w += "**";
                    if (i == tryLoc.WordNum - depth)
                        w = "**" + w;
                    descLine += w + ' ';
                }
                if (tryLoc.WordNum - depth < 0)
                    descLine = "**" + descLine;
                //lastDesc = descLine + "\r\n" + lastDesc;
                descLines.Insert(0, descLine);
                lineNums.Insert(0, tryLoc.LineNum);

                for (int i = 1; i <= depth; i++)
                {
                    if (i > tryLoc.WordNum)
                    {
                        doLoop = false;
                        break;
                    }
                    ret = wList[tryLoc.WordNum - i] + " " + ret;
                }
                // if we made it to the beginning of a sentence, break
                if (depth == tryLoc.WordNum)
                    doLoop = false;
            }

            if (ret.Length > 0)
            {
                for (int i = 1; i < descLines.Count; i++)
                {
                    if (lineNums[i - 1] == lineNums[i])
                    {
                        descLines[i - 1] = descLines[i - 1].Remove(descLines[i - 1].LastIndexOf("**"), 2).Insert(descLines[i].LastIndexOf("**"), "**");
                        lineNums.RemoveAt(i);
                        descLines.RemoveAt(i);
                        i--;
                    }
                }
                //if (descLines.Count > 1 && descLines[descLines.Count - 1].EndsWith(descLines[descLines.Count - 2].Substring(descLines[descLines.Count - 2].LastIndexOf(' '))))
                //    descLines.RemoveAt(descLines.Count - 1);
                if (descLines.Count > 1 && Regex.IsMatch(descLines[descLines.Count - 1], "\\*\\*\\S*\\*\\*$"))
                    descLines.RemoveAt(descLines.Count - 1);
                if (descLines.Count > 1 && Regex.IsMatch(descLines[0], "^\\*\\*\\S*\\*\\*"))
                    descLines.RemoveAt(0);
                lastDesc = descLines[0];
                for (int i = 1; i < descLines.Count; i++)
                    lastDesc += "\r\n" + descLines[i];
            }

            return ret;
        }

        public string getLastDesc(string arg)
        {
            return lastDesc;
        }

        public string saveMem(string filename)
        {
            StreamWriter file = new StreamWriter(filename, false);
            foreach (string line in lines)
                file.Write(line + '\n');
            file.Close();
            return "";
        }

        private static string getFormattedString(string s)
        {
            // convert to lower, remove new lines and quotes, hack to save !?
            s = s.ToLower()
                //.Replace("\r", "")
                //.Replace("\n", "")
                .Replace("\"", "").Replace("? ", "?. ").Replace("! ", "!. ");
            // Remove mentions
            s = Regex.Replace(s, "<@!\\d*> ?", "");
            s = Regex.Replace(s, "@\\w* ?", ""); // order this way to not accidentally get <@#> style without removing <>
            return s;
        }

        private static string getPurgedString(string s)
        {
            s = Regex.Replace(s, "<!\\d*> ?", "");
            return s;
        }

        private int getNumContexts()
        {
            int i = 0;
            foreach (string key in words.Keys) i += words[key].Count;
            return i;
        }

        private string getWordStatus(string s)
        {
            // This has a string arg to fit something else; ignore s
            int i = getNumContexts();
            int w = words.Count;
            double cpw = Math.Round((double)i / (double)w, 1);

            return "I know " + w + " words (" + i + " contexts, " + cpw + " per word), " + lines.Count + " lines.";
        }

        private string getContexts(string inp)
        {
            if (inp == null || inp == "")
                return "Not enough parameters, usage: !contexts <word>";
            if (inp.IndexOf(' ') != inp.Length - 1 && inp.IndexOf(' ') != -1)
                return "Too many parameters, usage: !contexts <word>";
            inp = inp.ToLower();
            if (!words.ContainsKey(inp))
                return inp + " is unknown.";
            string r = inp + " has " + words[inp].Count + " contexts:\r\n";
            for (int i = 0; i < words[inp].Count && r.Length < 1000; i++)
            {
                if (i < 1 || words[inp][i].LineNum != words[inp][i - 1].LineNum)
                    r += lines[words[inp][i].LineNum] + "\r\n";
            }
            return r;
        }

        public string getHelp(string inp)
        {
            if (inp.Length > 0)
            {
                if (commands.ContainsKey(inp)) return commands[inp].Help;
                return "Command " + inp + " not found";
            }
            string r = "Bot Commands:\r\n";
            foreach (string key in commands.Keys)
                if (commands[key].Desc.Length > 0) r += "\t" + key + ": " + commands[key].Desc + "\r\n";
            return r;
        }

        private string getKnown(string inp)
        {
            if (inp == null || inp == "")
                return "Not enough parameters, usage: !known <word(s)>";
            string[] inpws = inp.ToLower().Split(' ');
            string ret = "";
            foreach (string w in inpws)
            {
                if (!words.ContainsKey(w))
                    ret += w + " is unknown\r\n";
                else
                    ret += w + " is known (" + words[w].Count + " contexts)\r\n";
            }
            return ret;
        }

        private string doFeature(string feature)
        {
            if (feature == null || feature == "")
                return "Usage: !feature <text>";
            string filename = "featurelist.txt";
            if (new System.IO.FileInfo(filename).Length > 1000000)
                return "Feature list too long!";
            StreamWriter writer = new StreamWriter(filename, true);
            writer.WriteLine(feature);
            writer.Close();
            return "Added to suggestion list.";
        }

        private string doWOD(string arg)
        {            
            if (DateTime.UtcNow.Date != lastDate.Date)
            {
                lastDate = DateTime.Now;

                string rword;
                do
                {
                    rword = words.Keys.ToList()[rand.Next(words.Keys.Count)];
                } while (rword.Length < WOD_LEN || rword == lastWOD || words[rword].Count < 2);

                lastWOD = rword;
            }
            string rphrase;
            do
            {
                rphrase = reply(lastWOD);
            } while (rphrase.Length < WOD_EXM);

            return "The Word of the Day for " + lastDate.ToShortDateString() + " is '" + lastWOD + "', as in, \"" + rphrase + "\"";
        }

        public override string loadMem()
        {
            return loadMem(filename);
        }

        public override string saveMem()
        {
            return saveMem(filename);
        }

        public static string statLoadMem(string filename)
        {
            try
            {
                StreamReader file = new StreamReader(filename);
                while (!file.EndOfStream)
                    learnSingle(getPurgedString(file.ReadLine()));
                file.Close();
            }
            catch (IOException ex)
            {
                return ex.Message + "; creating new dictionary";
            }
            return "";
        }

        public static string statSaveMem(string filename)
        {
            StreamWriter file = new StreamWriter(filename, false);
            foreach (string line in lines)
                file.Write(line + '\n');
            file.Close();
            return "";
        }
    }
}