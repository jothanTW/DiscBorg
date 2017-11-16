using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordDSPTestConnect
{
    class BattleshipGame : DSPModule
    {
        private const int map_width = 10;
        private static Random rand;
        private static string[] hitText;
        private static string[] missText;
        private static char nHit = ' ', mHit = '-', dHit = 'X';

        static BattleshipGame()
        {
            rand = new Random();
            hitText = new string[] { "Hit!", "Hit", "Hit", "...hit.", "_hit_", "hiiit", "Nice Hit!", "gah, hit"};
            missText = new string[] { "Miss!", "Miss", "Miss", "_miss_", "Miss.", "Miss, but you creamed a seagull along the way.", "LOL nice miss", "_FAILURE_", "Try Again!" };
        }

        private class Ship
        {
            public Ship(string s, int l, int x, int y, bool r)
            {
                name = s;
                size = l;
                this.x = x;
                this.y = y;
                rotate = r;
                hits = new bool[size];
            }
            public int getTileOn(int x, int y)
            {
                int s = -1;
                if (this.x == x && rotate)
                    s = y - this.y;
                else if (this.y == y && !rotate)
                    s = x - this.x;
                if (s < size && s > -1)
                    return s;
                return -1;
            }
            public int getHits()
            {
                int h = 0;
                for (int i = 0; i < size; i++)
                    if (hits[i]) h++;
                return h;
            }
            public bool doHit(int x, int y)
            {
                int h = getTileOn(x, y);
                if (h < 0)
                    return false;
                if (!hits[h])
                {
                    hits[h] = true;
                    return true;
                }
                return false;
            }
            public bool collide(Ship s)
            {
                for (int i = 0; i < size; i++)
                {
                    if (rotate)
                    {
                        if (s.getTileOn(x, y + i) != -1)
                            return true;
                    }
                    else
                    {
                        if (s.getTileOn(x + i, y) != -1)
                            return true;
                    }
                }
                return false;
            }
            private string name;
            private int size;
            private bool[] hits;
            private int x, y;
            private bool rotate; // x is same, extends along y
            public string Name { get { return name; } }
            public bool Sunk { get { return getHits() == size; } }
            public int X { get { return x; } }
            public int Y { get { return y; } }
        }

        private int shots;
        private List<Ship> ships;
        private bool gameWon;
        private char[] hitRecord;

        public BattleshipGame() : base("Battleship")
        {

            commands.Add("battleship", new ComObj("battleship", "Play a game.",
                "Added battleship for blanko. " + BattleshipGame.Usage, doShot));
            commands.Add("bs", new ComObj("bs", "",
                "Shortcut for !battleship", doShot));
            commands.Add("new", new ComObj("new", "Starts a new game", "Starts a new game of battleship", doNew));
            commands.Add("map", new ComObj("map", "Displays the map", "Displays the shot map", doMap));

            init();
        }

        private void init()
        {
            shots = 0;
            ships = new List<Ship>();

            // place ships
            addShip("Carrier", 5);
            addShip("Battleship", 4);
            addShip("Cruiser", 3);
            addShip("Submarine", 3);
            addShip("Destroyer", 2);

            hitRecord = new char[map_width * map_width];
            for (int i = 0; i < map_width * map_width; i++)
                hitRecord[i] = nHit;

            gameWon = false;
        }

        private bool checkWin()
        {
            foreach (Ship s in ships)
                if (!s.Sunk)
                    return false;
            return true;
        }

        private string win()
        {
            gameWon = true;
            string w = "You've won!\r\nTurns taken: " + shots + ". ";
            // ranges: 1/4, 1/2, 3/4, 1, >1
            double shotPerc = (double) shots / (double)(map_width * map_width);
            //if (shots == 17)
            //    w += "That's a perfect score. Your username has been logged for cheating.";
            if (shotPerc < 0.25)
                w += "That's an extremely good score! Your username has been logged for cheating.";
            else if (shotPerc < 0.5)
                w += "That's not bad!";
            else if (shotPerc < 0.75)
                w += "That's nothing special.";
            else if (shotPerc < 1)
                w += "That's honestly pretty bad.";
            else
                w += "You took more shots than there are tiles.";
            return w;
        }

        private void addShip(string name, int size)
        {
            while (true)
            {
                bool rotate = (rand.Next(2) == 0);
                int x, y;
                if (rotate)
                {
                    x = rand.Next(map_width);
                    y = rand.Next(map_width - size + 1);
                }
                else
                {
                    y = rand.Next(map_width);
                    x = rand.Next(map_width - size + 1);
                }
                Ship s = new Ship(name, size, x, y, rotate);
                if (collideAll(s))
                    continue;
                ships.Add(s);
                break;
            }
        }

        private bool collideAll(Ship s)
        {
            foreach (Ship ship in ships)
                if (ship.collide(s))
                    return true;
            return false;
        }

        private string statusStr()
        {
            if (gameWon)
                return "Game won! " + shots + " shots taken. Use !battleship new to start again.";
            string r = "Map size: " + map_width + " by " + map_width + "\r\nShots fired: " + shots + "\r\nShips remaining:";
            foreach (Ship s in ships) if (!s.Sunk) r += " " + s.Name + ",";
            return r.Substring(0, r.Length - 1) + "\r\n" 
                //+ getMap() + "\r\n" 
                + Usage;
        }

        public static string Usage { get { return "Usage: !battleship <A-" + (char)('A' + map_width - 1) + "><1-" + map_width + ">."; } }

        public string doShot(string input)
        {
            if (input.Length == 0) return statusStr();
            if (gameWon)
                return "Game's over. Use !new to restart.";
            input = System.Text.RegularExpressions.Regex.Replace(input, "[-:_]", "");
            try
            {
                int y = input.ToUpper()[0] - 'A';
                int x = Convert.ToInt32(input.Substring(1)) - 1;
                if (!(y < 0 || y >= map_width || x < 0 || x >= map_width))
                {
                    shots++;
                    if (hitRecord[x + y * map_width] == nHit)
                    {
                        foreach (Ship s in ships)
                        {
                            if (s.doHit(x, y))
                            {
                                hitRecord[x + y * map_width] = dHit;
                                string r = hitText[rand.Next(hitText.Length)];// + " (" + s.Name + ")";
                                if (s.Sunk)
                                {
                                    r += "\r\nYou sunk my " + s.Name + "!";
                                    if (checkWin()) r += "\r\n" + win();
                                }
                                return r;
                            }
                        }
                        hitRecord[x + y * map_width] = mHit;
                    }
                    return missText[rand.Next(missText.Length)];
                }
            }
            catch (Exception e)
            {
                if (!(e is FormatException || e is IndexOutOfRangeException))
                    Console.Out.WriteLine(e.GetType() + ": " + e.Message);
            }
            return Usage;
        }

        public string doNew(string input)
        {
            string r = "New game loaded!";
            if (!gameWon) r += "You used " + shots + " last game.";
            init();
            return r;
        }

        public string doMap(string input)
        {
            return getMap();
        }

        public string command_(string input){
            if (input.Length == 0) return statusStr();
            if (input.ToLower().Equals("new"))
            {
                init();
                string r = "New game loaded!";
                if (!gameWon) r += " You used " + shots + " shots last game.";
                return r;
            }
            if (gameWon)
                return "Game's over. Use !battleship new to restart.";
            try
            {
                int y = input.ToUpper()[0] - 'A';
                int x = Convert.ToInt32(input.Substring(1)) - 1;
                if (!(y < 0 || y >= map_width || x < 0 || x >= map_width))
                {
                    shots++;
                    foreach (Ship s in ships)
                    {
                        if (s.doHit(x, y))
                        {
                            hitRecord[x + y * map_width] = dHit;
                            string r = hitText[rand.Next(hitText.Length)];
                            if (s.Sunk) r += "\r\nYou sunk my " + s.Name + "!";
                            return r;
                        }
                        else
                        {
                            hitRecord[x + y * map_width] = mHit;
                            return missText[rand.Next(missText.Length)];
                        }
                    }
                }
            } catch (Exception e)
            {
                if (!(e is FormatException || e is IndexOutOfRangeException))
                    Console.Out.WriteLine(e.GetType() + ": " + e.Message);
            }
            return Usage;
        }

        private string getMap()
        {

            string map = "```  ";
            for (char c = 'A'; c < 'A' + map_width; c++)
                map += " " + c;
            // build the lines
            for (int i = 1; i <= map_width; i++)
            {
                map += "\r\n";
                    char tens = (char)('0' + i / 10);
                if (tens == '0') tens = ' ';
                map += tens;
                map += (char)('0' + (i % 10));
                for (int j = 0; j < map_width; j++)
                    map += " " + hitRecord[j * map_width + i - 1];
            }
            return map + "```";
        }

        public override string loadMem()
        {
            return "";
        }

        public override string saveMem()
        {
            return "";
        }
    }
}
