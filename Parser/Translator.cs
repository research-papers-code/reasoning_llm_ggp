using Parser.Simulator;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
	public class Translator
    {
        public static Dictionary<string, short> Dictionary;
        public static Dictionary<string, int> FactNames;

        public static short _seed;
        private static Translator unique = new Translator();


        public static short BlankValue = -1;
        public static string BlankSymbol = "][ blank";

		public static int RoleCount;

        public void InsertBlank()
        {
            if (Dictionary.ContainsKey(BlankSymbol) == false)
            {
                InsertSymbol(BlankSymbol);
                BlankValue = Dictionary[BlankSymbol];
            }
        }
        public override string ToString()
        {
           StringBuilder builder = new StringBuilder();
           foreach (var entry in Dictionary)
               builder.AppendLine(string.Format("({0}->{1})", entry.Key, entry.Value));
           builder.AppendLine("--- facts ----");
           foreach (var entry in FactNames)
               builder.AppendLine(string.Format("({0}->{1})", entry.Key, entry.Value));
           return builder.ToString();
        }

        public static Translator Instance
        {
            get
            {
                if (unique == null)
                    unique = new Translator();
                return unique;
            }
        }

        public Translator()
        {
            Clean();
        }

        public void Clear()
        {
            Dictionary.Clear();
            unique = new Translator();
            _seed = 0;
        }

        public static void Clean()
        {
            Dictionary = new Dictionary<string, short>();
            _seed = 0;
            FactNames = new Dictionary<string, int>();
            FactNames.Add("does", 0);
        }

        public void InsertSymbol(string symbol)
        {
            if (symbol[0] == '?')
                return;

            if (Dictionary.ContainsKey(symbol) == false)
            {
                Dictionary.Add(symbol, _seed++);
            }
        }

        public short ToValueDefault(string symbol, short defaultValue)
        {
            short returnValue;
            if (Dictionary.TryGetValue(symbol, out returnValue))
                return returnValue;
            else
                return defaultValue;
        }

        public short[] ToValuesArray(string[] symbolArray)
        {
            short[] valueArray = new short[symbolArray.Length];
            for (int i = 0; i < symbolArray.Length; ++i)
                valueArray[i] = ToValue(symbolArray[i]);
            return valueArray;
        }

        public bool TryToValue(string symbol, out short value)
        {
            return Dictionary.TryGetValue(symbol, out value);
        }

        public short ToValue(string symbol)
        {
			if(Dictionary.ContainsKey(symbol)==false)
			{
				if (Dictionary.ContainsKey(symbol.ToLower()))
					return Dictionary[symbol.ToLower()];
				if(Dictionary.ContainsKey(symbol.ToUpper()))
					return Dictionary[symbol.ToUpper()];

				TextState.BugLogger.Log(BugType.INVALID_SYMBOL, -1,-1, $"Invalid symbol: {symbol}", 0.0);
			}
            return Dictionary[symbol];
        }

        public string ToSymbol(short value)
        {
            KeyValuePair<string, short> first;
            first = Dictionary.FirstOrDefault(a => a.Value == value);
            return first.Key;
        }

        public int FactToValue(string factName)
        {
            int factValue;
            if (FactNames.TryGetValue(factName, out factValue))
                return factValue;

            FactNames.Add(factName, FactNames.Count);
            return FactNames.Count - 1;
        }

        public string ToFactName(int factValue)
        {
            KeyValuePair<string, int> first;
            first = FactNames.FirstOrDefault(a => a.Value == factValue);
            return first.Key;
        }

        public string ActionToText(short[] row)
        {
            if (row.Length < 2)
                return "";

            string actionName = ToSymbol(row[1]);
            if (row.Length == 2)
                return actionName;

            StringBuilder builder = new StringBuilder("(");
            builder.Append(actionName);

            bool atLeastOneRealArgument = false;
            for (int i = 2; i < row.Length; ++i)
            {
                if (row[i] != BlankValue)
                {
                    atLeastOneRealArgument = true;
                    builder.Append(" ");
                    builder.Append(ToSymbol(row[i]));
                }
            }
            builder.Append(")");
			return atLeastOneRealArgument ? builder.ToString() : actionName;
        }

        public string RowToText(short[] row)
        {
            if (row.Length == 0)
                return "";
            string text = ToSymbol(row[0]);
            for (int i = 1; i < row.Length; ++i)
            {
                if (row[i] != BlankValue)
                    text += (" " + ToSymbol(row[i]));

            }
            return text;
        }

        public void InsertArgument(GDLArgument arg)
        {
            if (!arg.Constant)
                return;

            if (arg.Compound == false)
            {
                if (Dictionary.ContainsKey(arg.Name) == false)
                {
                    Dictionary.Add(arg.Name, _seed++);
                }
            }
            else
            {
                foreach (GDLArgument child in arg.Children)
                    InsertArgument(child);
            }
        }

        public void CheckRoleNames(List<string> RoleNames)
        {
			RoleCount = RoleNames.Count;
            for (short roleIndex = 0;  roleIndex < RoleNames.Count; ++roleIndex)
            {
                string roleName = RoleNames[roleIndex];
                short value = Dictionary[roleName];
                if (value != roleIndex)
                {
                    string symbol = ToSymbol(roleIndex);
                    Dictionary[symbol] = value;
                    Dictionary[roleName] = roleIndex;
                }
            }
        }
    }
}
