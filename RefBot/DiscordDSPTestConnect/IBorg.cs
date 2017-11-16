using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordDSPTestConnect
{
    interface IBorg
    {
        string reply(string input);
        string learn(string input);
        string command(string input, bool isAdmin);
        string loadMem(string filename);
        string saveMem(string filename);
        string getHelp(string inp);
    }
}
