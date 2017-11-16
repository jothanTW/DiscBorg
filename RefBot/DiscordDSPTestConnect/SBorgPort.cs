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

        private Random rand;
        private List<string> lines;
        private Dictionary<string, List<WordLoc>> words;
        //private Dictionary<string, ComObj> commands;
        private int minDepth;
        private int maxDepth;

        public SBorgPort() : base("borg")
        {
            rand = new Random();
            minDepth = 1;
            maxDepth = 4;
            lines = new List<string>();
            words = new Dictionary<string, List<WordLoc>>();
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

        private void learnSingle(string input) // for post-formatted lines
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

        public string learn(string input)
        {
            string[] s = getFormattedString(input).Split( new string[] { ". " }, StringSplitOptions.RemoveEmptyEntries);
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
            List<string> wds = new List<string>(getFormattedString(input).Replace(".", "").Split(new char[] { ' ' }));
            for (int i = 0; i < wds.Count; i++)
                if (!words.ContainsKey(wds[i]))
                    wds.RemoveAt(i--);

            if (wds.Count == 0)
                wds.Add(words.Keys.ToList()[rand.Next(words.Keys.Count)]);

            string ret = wds[rand.Next(wds.Count)];
            bool doLoop = true;
            while (doLoop) // right edge build
            {
                string thisWord = ret.Substring(ret.LastIndexOf(' ') + 1);
                WordLoc tryLoc = words[thisWord][rand.Next(words[thisWord].Count)];
                string[] wList = lines[tryLoc.LineNum].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int depth = rand.Next(maxDepth - minDepth) + minDepth;
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
            return ret;
        }



        public string saveMem(string filename)
        {
            StreamWriter file = new StreamWriter(filename, false);
            foreach (string line in lines)
                file.Write(line + '\n');
            file.Close();
            return "";
        }

        private string getFormattedString(string s)
        {
            // convert to lower, remove new lines and quotes, hack to save !?
            s = s.ToLower().Replace("\r", "").Replace("\n", "").Replace("\"", "").Replace("? ", "?. ").Replace("! ", "!. ");
            // Remove mentions
            s = Regex.Replace(s, "<@\\d*> ?", "");
            s = Regex.Replace(s, "@\\w* ?", ""); // order this way to not accidentally get <@#> style without removing <>
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
            // this is procedural based on day, get number representing the day
            int d = (DateTime.UtcNow - new DateTime(1970, 1, 1)).Days;
            // backup and reseed generator
            Random oldRand = rand;
            rand = new Random(d);

            // Might make this datetime an argument?
            DateTime t = DateTime.Now;
            
            string rword;
            string rphrase;

            do
            {
                rword = words.Keys.ToList()[rand.Next(words.Keys.Count)];
            } while (rword.Length < WOD_LEN);
            do
            {
                rphrase = reply(rword);
            } while (rphrase.Length < WOD_EXM);

            rand = oldRand;
            return "The Word of the Day for " + t.ToShortDateString() + " is '" + rword + "', as in, \"" + rphrase + "\"";
        }

        public override string loadMem()
        {
            return loadMem(filename);
        }

        public override string saveMem()
        {
            return saveMem(filename);
        }
        /*
// dice chars
private const char DV_DI = 'd';
private const char DV_IT = '#';
private const char DV_DR = 'l';
private const char DV_KP = 'k';
private const char DV_XP = 'x';
private const char DV_TR = 't';

private static int MAX_DICE_ITER = 10;

private static string removeWhitespace(string text)
{
   return text.Replace(" ", "");
}

private static int getNumber(string text, ref int offset)
{
   int val = 0;
   while (offset < text.Length)
   {
       if (text[offset] < '0' || text[offset] > '9')
           break;
       val *= 10;
       val += text[offset] - '0';
       offset++;
   }
   return val;
}

private static bool isLegal(char c)
{
   string legalDiceChars = "0123456789+-dklt";
   int iter = 0;
   while (iter < legalDiceChars.Length)
   {
       if (c == legalDiceChars[iter])
           return true;
       iter++;
   }
   return false;
}

private int rollFromSeg(string text)
{
   int val = 0;
   bool doNeg = (text[0] == '-');
   if (text[0] == '+' || text[0] == '-') { text = text.Substring(1); }
   // Expected format: #d#f# (startval)(diceval)(flagval)
   // if:
   //   #: return #
   //   d#<f#> assume 1d#<f#>
   //   <d># or <f># throw

   int startval, diceval;
   int place = 0;
   if (text[0] != 'd' && text[0] < '0' && text[0] > '9')
       throw new Exception("Parse error: unexpected character " + text[0]);
   if (text[0] == 'd')
       startval = 1;
   else
       startval = getNumber(text, ref place);

   if (place == text.Length)
       val = startval;
   else
   {
       if (text[place] != 'd')
           throw new Exception("Parse error: unexpected character " + text[place]);
       place++;
       diceval = getNumber(text, ref place);
       if (diceval <= 0)
           throw new Exception("Parse error: dice size must be 1 or greater");

       int[] diceSet = new int[startval];
       for (int i = 0; i < startval; i++)
           diceSet[i] = (rand.Next(diceval)) + 1;

       // get flag
       if (place == text.Length)
       {
           // no flag
           for (int i = 0; i < startval; i++)
               val += diceSet[i];
       }
       else
       {
           char f = text[place];
           place++;
           if (place == text.Length)
               throw new Exception("Error: expected value after flag " + f);
           int flagval = getNumber(text, ref place);
           switch (f)
           {
               case DV_TR:
                   // target
                   for (int i = 0; i < startval; i++)
                       if (diceSet[i] >= flagval)
                           val++;
                   break;
               case DV_XP:
                   // exploding dice (one iteration)
                   for (int i = 0; i < startval; i++)
                   {
                       val += diceSet[i];
                       if (diceSet[i] >= flagval)
                           val += (rand.Next(diceval)) + 1;
                   }
                   break;
               case DV_KP:
                   // Get <flagval> highest numbers
                   if (flagval > startval) flagval = startval;
                   for (int i = 0; i < flagval; i++)
                   {
                       int highIter = 0;
                       for (int j = 1; j < startval; j++)
                       {
                           if (diceSet[j] > diceSet[highIter])
                               highIter = j;
                       }
                       val += diceSet[highIter];
                       diceSet[highIter] = 0;
                   }
                   break;
               case DV_DR:
                   // Get <flagval> lowest numbers
                   if (flagval > startval) flagval = startval;
                   for (int i = 0; i < flagval; i++)
                   {
                       int lowIter = 0;
                       for (int j = 1; j < startval; j++)
                       {
                           if ((diceSet[j] < diceSet[lowIter] && diceSet[j] != 0) || diceSet[lowIter] == 0)
                               lowIter = j;
                       }
                       val += diceSet[lowIter];
                       diceSet[lowIter] = 0;
                   }
                   break;
               default:
                   throw new Exception("Error: unknown flag " + f);
           }
       }
   }

   if (doNeg) val *= -1;
   return val;
}

private int rollFromString(string text)
{
   // Do Roll
   // Assume :
   // Everything's already in lower case
   // No parenthesis or brackets
   // no mul/div
   // no arbitrary dice size or amount (i.e. 2d4d4)

   int val = 0;
   // First: split into discrete segments of one set of dice each
   string[] rolls = text.Split(new char[] { '+', '-' });
   string vText;
   if (text[0] == '+' || text[0] == '-')
       vText = text;
   else
       vText = '+' + text;
   int iter = 0; // points to next +-

   foreach (string str in rolls)
   {
       if (vText[iter] == '-')
           val += rollFromSeg('-' + str);
       else val += rollFromSeg(str);
       iter += str.Length + 1;
   }
   return val;
}

string doRoll(string text)
{
   // Prep roll string
   string val = removeWhitespace(text).ToLower();

   if (val == "")
       return commands["roll"].Help;

   string fin = "rolled: ";
   fin += text;
   fin += ": ";
   try
   {
       // Get Iterations
       int place = 0;
       int iterate = getNumber(val, ref place);
       bool isIt = false;
       if (val[place] == DV_IT)
       {
           // remove the iterator
           place++;
           val = val.Substring(place);
           isIt = true;
       }
       if (iterate > 1 && isIt)
       {
           if (iterate > MAX_DICE_ITER) iterate = MAX_DICE_ITER;
           for (int i = 0; i < iterate; i++)
           {
               fin += "\r\n";
               fin += (i + 1);
               fin += "#\t";
               fin += (rollFromString(val));
           }
       }
       else
       {
           fin += (rollFromString(val));
       }
   }
   catch (Exception ex)
   {
       return ex.Message;
   }

   return fin;
}*/
    }
}
