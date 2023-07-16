using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordDSPTestConnect
{
    class DiceRoller : DSPModule
    {
        private static Random rand = new Random();

        // dice chars
        private const char DV_DI = 'd';
        private const char DV_IT = '#';
        private const char DV_DR = 'l';
        private const char DV_KP = 'k';
        private const char DV_XP = 'x';
        private const char DV_TR = 't';

        private static int MAX_DICE_ITER = 10;

        public DiceRoller(string n) : base(n) 
        {
            commands.Add("roll", new ComObj("roll", "Roll dice, use !help roll for more info",
                   "Roll dice. Usage: !roll <dice command>. Expected format: <I>#<N>d<S><f><F>, where I is the number of times to roll, N is the number of dice, "
                   + "S is the dice size, f is any flag, and F is the flag argument. '<I>#', '<N>', '<N>d' are optional and implied; '<f><F>' is optional. Flags:\r\n"
                   + "k: keep <F> highest rolls of NdS\r\n"
                   + "l: keep <F> lowest rolls of NdS\r\n"
                   + "x: reroll dice results larger than or equal to <F>, once\r\n"
                   + "t: count dice results larger than or equal to <F>", doRoll));
        }

        public override string loadMem()
        {
            return "";
        }

        public override string saveMem()
        {
            return "";
        }

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
            /* Assume :
             * Everything's already in lower case
             * No parenthesis or brackets
             * no mul/div
             * no arbitrary dice size or amount (i.e. 2d4d4)
             */
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
        }
    }
}
