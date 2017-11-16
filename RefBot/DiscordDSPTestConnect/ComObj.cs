using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordDSPTestConnect
{
    class ComObj
    {
        private string name, desc, help;
        private bool needAdmin;
        Func<string, string> action;

        public ComObj(string n, string d, string h, Func<string, string> a, bool na = false)
        {
            name = n;
            desc = d;
            action = a;
            help = h;
            needAdmin = na;
        }
        public string doAction(string inp = "", bool isAdmin = false)
        {
            if (needAdmin && !isAdmin)
                return "";
            if (inp == null)
                inp = "";
            return action(inp);
        }
        public string Desc
        {
            get { return desc; }
        }
        public string Help
        {
            get { return help; }
        }
        public bool Does
        {
            get { return action != null; }
        }
    }
}
