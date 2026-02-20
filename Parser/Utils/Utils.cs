using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Parser.Simulator;

namespace Parser
{
    public class Utils
    {
        #region Tokenization
        public static GDLRuleheet ParseRules(string message)
        {
            GDLParser parser = new GDLParser();
            GDLRuleheet rs = parser.ParseFromMessage(message);
            return rs;
        }
        #endregion

        public static GDLSimulator CreateSimulatorFromPath(string path, int simulatorID, int interpretersCount)
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
            return Utils.CreateSimulator(msg, simulatorID, interpretersCount);
        }

        public static GDLSimulator CreateSimulator(string message, int simulatorID, int interpretersCount)
        {
            GDLRuleheet rs = ParseRules(message);
            if (simulatorID == 0 && interpretersCount > 0)
            {
                CppConnector.Simulator_Initialize(rs.PlayersCount, interpretersCount);
            }
            var compiler = new GDLCompiler(simulatorID, rs);
            return compiler.Compile();
        }
       
        public static int TickCountDifference(int firstTick, int secondTick)
        {
            if (secondTick <= 0 && firstTick > 0)
            {


            }
            return secondTick - firstTick;
        }

        public static void Log2File(string message, bool autoFlush = false)
        {
            try
            {
                StreamWriter writer = new StreamWriter("log.txt", true);
                writer.WriteLine(message);
                if (autoFlush)
                    writer.Flush();
                writer.Close();
            }
            catch (Exception)
            {

            }
        }

        public static void Log2File(List<string> list, bool autoFlush = false)
        {
            try
            {
                StreamWriter writer = new StreamWriter("log.txt", true);
                foreach(string message in list)
                    writer.WriteLine(message);
                if (autoFlush)
                    writer.Flush();
                writer.Close();
            }
            catch (Exception)
            {

            }
        }

        public static void Log2File(string filename, string message, bool autoFlush = true)
        {
            try
            {
                StreamWriter writer = new StreamWriter(filename + ".txt", true);
                writer.WriteLine(message);
                if (autoFlush)
                    writer.Flush();
                writer.Close();
            }
            catch (Exception)
            {

            }
        }

        public static string NormalizeBrackets(string input)
        {
            input = input.Replace("( ", "(");
            input = input.Replace(" )", ")");
            return input;
        }

        public static string TrimBrackets(string input)
        {
            return input.Replace('(', ' ').Replace(')', ' ').Trim();
        }

        public static string ReadUncommented(string filename)
        {
            StringBuilder builder = new StringBuilder();
            StreamReader reader = new StreamReader(filename);
            string body = reader.ReadToEnd();
            reader.Close();

            bool removeMode = false;
            for (int i = 0; i < body.Length; ++i)
            {
                char c = body[i];
                if (removeMode)
                {
                    if (c == '\r' || c == '\n')
                        removeMode = false;
                }
                else
                {
                    if (c == ';')
                        removeMode = true;
                    else
                        builder.Append(c);
                }
            }

            return builder.ToString().Replace("( ", "(").Replace(") ", ")").Replace(" )", ")").Replace(" (", "(");
        }

        public static List<int> RandomIndices(int count, int maxValue)
        {
            Random random = new Random();
            List<int> randomIndices = new List<int>();
            while (randomIndices.Count < count)
            {
                int value = random.Next(maxValue);
                if (randomIndices.Contains(value))
                {
                    bool contains = true;
                    while (contains)
                    {
                        value = (value + 1) % maxValue;
                        contains = randomIndices.Contains(value);
                    }
                }
                randomIndices.Add(value);

            }
            return randomIndices;
        }
    }
}
