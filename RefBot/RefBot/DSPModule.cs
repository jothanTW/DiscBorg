using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordDSPTestConnect
{
    class DSPModuleMapMaker
    {
        private struct ModuleLink { public bool isStatic; public int index; public string name; }
        private List<ModuleLink> modLinks;
        private List<DSPModule> staticMods;
        private List<Type> indivMods;

        public DSPModuleMapMaker()
        {
            modLinks = new List<ModuleLink>();
            staticMods = new List<DSPModule>();
            indivMods = new List<Type>();
        }

        public DSPModuleMap getNewModuleMap()
        {
            DSPModuleMap map = new DSPModuleMap();
            foreach(ModuleLink link in modLinks)
            {
                if (link.isStatic)
                    map.add(staticMods[link.index]);
                else
                    map.add((DSPModule)Activator.CreateInstance(indivMods[link.index], link.name));
            }
            return map;
        }

        public void addStaticModule(DSPModule module)
        {
            ModuleLink link = new ModuleLink();
            link.isStatic = true;
            link.index = staticMods.Count;
            staticMods.Add(module);
            modLinks.Add(link);
        }

        public void addIndivModule(Type type, string name)
        {
            if (type.IsSubclassOf(typeof(DSPModule)))
            {
                ModuleLink link = new ModuleLink();
                link.isStatic = false;
                link.index = indivMods.Count;
                link.name = name;
                indivMods.Add(type);
                modLinks.Add(link);
            }
        }
    }

    class DSPModuleMap
    {
        public const char MOD_ID = '$';
        private Dictionary<string, DSPModule> map;

        public DSPModuleMap()
        {
            map = new Dictionary<string, DSPModule>();
        }

        public DSPModule this[string name]
        {
            get { return map[name]; }
        }

        public bool add(DSPModule module)
        {
            if (map.ContainsKey(module.Name))
                return false;
            map.Add(module.Name, module);
            return true;
        }

        public int numCommands(string com)
        {
            int r = 0;
            foreach (string key in map.Keys)
                if (map[key].hasCommand(com)) r++;
            return r;
        }

        public string command(string input, bool isAdmin)
        {
            string[] args = input.Split(' ');
            if (args[0][0] == MOD_ID) // identify the module
            {
                if (map.ContainsKey(args[0].Substring(1)))
                    return map[(args[0].Substring(1))].command(input.Substring(input.IndexOf(' ') + 1), isAdmin);
                return "";
            }
            // find the command
            foreach (string key in map.Keys)
                if (map[key].hasCommand(args[0].Substring(1)))
                    return map[key].command(input, isAdmin);
            return "";
        }

        public string getHelp(string input)
        {
            if (input.Length > 0)
            {
                foreach (DSPModule module in map.Values)
                {
                    if (module.hasCommand(input))
                        return module.getHelp(input);
                }
                return "Command not found.";
            }
            string r = "";
            foreach (DSPModule module in map.Values)
                r += module.getHelp(input);
            return r;
        }
    }
    abstract class DSPModule
    {
        protected Dictionary<string, ComObj> commands;
        protected string filename;
        protected string name;
        
        public abstract string loadMem();
        public abstract string saveMem();

        public DSPModule(string n)
        {
            commands = new Dictionary<string, ComObj>();
            filename = n + ".dat";
            name = n;
        }

        public string getHelp(string inp)
        {
            if (inp.Length > 0)
            {
                if (commands.ContainsKey(inp)) return commands[inp].Help;
                return "Command " + inp + " not found";
            }
            string r = name + " Commands:\r\n";
            foreach (string key in commands.Keys)
                if (commands[key].Desc.Length > 0) r += "\t" + key + ": " + commands[key].Desc + "\r\n";
            return r;
        }

        public string command(string input, bool isAdmin = false)
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
        }

        public string Filename { get { return filename; } set { filename = value; } }

        public string Name {  get { return name; } }

        public bool hasCommand(string com)
        {
            com = com.Split(' ')[0];
            if (com[0] == '!') com = com.Substring(1);
            return commands.ContainsKey(com);
        }
    }
}
