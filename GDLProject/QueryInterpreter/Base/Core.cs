using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.IO;
using QueryInterpreter.Messaging;
using System.Threading;
using QueryInterpreter._Tree;
using QueryInterpreter.Base;
using Parser.Simulator;
using Parser;

namespace QueryInterpreter
{
    public class Core
    {
        public GDLSimulator simulator;
        public IPEndPoint localEndpoint = null;
        public TcpListener localListener = null;
        public static Configuration Configuration;
        
        public Core()
        {
            
        }
        #region Tokenization
        public static GDLRuleheet ParseRules(string message)
        {
            GDLParser parser = new GDLParser();
            GDLRuleheet rs = parser.ParseFromString(message, false);
            return rs;
        }
        #endregion

        public static void Clean()
        {
            Translator.Clean();
        }

        public static GDLSimulator CreateSimulatorFromPath(string path, int simulatorID)
        {
            string file = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
            if (File.Exists(path) == false)
            {
                path = file + ".gdl";
                if (File.Exists(path) == false)
                {
                    path = file + ".kif";
                }
            }
            string msg = Utils.ReadUncommented(path);
            msg = Utils.NormalizeBrackets(msg);
            return Core.CreateSimulator(msg, simulatorID);
        }
        public static GDLSimulator CreateSimulator(string message, int simulatorID)
        {
            GDLRuleheet rs = ParseRules(message);
            var compiler = new GDLCompiler(0, rs);
            compiler.Compile();
        }
        
    }
}
