using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class GDLQuery
    {
        public List<GDLQuery_EqualVarSym> EqualSymbol = new List<GDLQuery_EqualVarSym>();
        public List<GDLQuery_EqualVarVar> EqualVariable = new List<GDLQuery_EqualVarVar>();

        public GDLQuery()
        {
            
        }

        public GDLQuery(GDLTerm condition)
        {
            Consume(condition);
        }

        public void FillRow(RowSet set, int rowPtr)
        {
            foreach (var entry in EqualVariable)
                set.Data[rowPtr + entry.Var1Index] = set.Data[rowPtr + entry.Var2Index];

            foreach (var entry in EqualSymbol)
                set.Data[rowPtr + entry.VarIndex] = entry.SymbolValue;

        }

		public bool PassRowInverse(RowSet set, int rowPtr)
		{
			foreach (var entry in EqualVariable)
			{
				if (set.Data[rowPtr + entry.Var1Index] == set.Data[rowPtr + entry.Var2Index])
					return false;
			}

			foreach (var entry in EqualSymbol)
			{
				if (set.Data[rowPtr + entry.VarIndex] == entry.SymbolValue)
					return false;
			}
			return true;
		}

		public bool PassRow(RowSet set, int rowPtr)
		{
			foreach (var entry in EqualVariable)
			{
				if (set.Data[rowPtr + entry.Var1Index] != set.Data[rowPtr + entry.Var2Index])
					return false;
			}

			foreach (var entry in EqualSymbol)
			{
				if (set.Data[rowPtr + entry.VarIndex] != entry.SymbolValue)
					return false;
			}
		
			return true;
		}

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            foreach (GDLQuery_EqualVarSym es in EqualSymbol)
            {
                builder.Append(es.ToString());
                builder.Append(" ");
            }
            foreach (GDLQuery_EqualVarVar ev in EqualVariable)
            {
                builder.Append(ev.ToString());
                builder.Append(" ");
            }
            builder.Append("}");
            return builder.ToString();
        }
        public int ConstraintsCount
        {
            get
            {
                return EqualSymbol.Count + EqualVariable.Count;
            }
        }

        public void Consume(GDLTerm term)
        {
            for(int i=0; i<term.Arguments.Count;++i)
            {
                GDLArgument arg = term.Arguments[i];
                if (arg.Compound)
                    throw new Exception("error");

                if (arg.Constant)
                {
                    EqualSymbol.Add(new GDLQuery_EqualVarSym(i, arg.Name));
                }
                else
                {
                    GDLArgument argFirst = term.Arguments.FirstOrDefault(a => a.Name == arg.Name);
                    if (argFirst != arg)
                    {
                        int index = term.Arguments.IndexOf(argFirst);
                        EqualVariable.Add(new GDLQuery_EqualVarVar(index, i));
                    }
                  
                }
            }
        }
    }

    public class GDLQuery_EqualVarVar
    {
        public int Var1Index = 0;
        public int Var2Index = 0;
        public GDLQuery_EqualVarVar(int v1Index, int v2Index)
        {
            if (v1Index > v2Index)
            {
                Var1Index = v1Index;
                Var2Index = v2Index;
            }
            else
            {
                Var1Index = v2Index;
                Var2Index = v1Index;
            }
        }
        public override string ToString()
        {
            return string.Format("[{0}]=[{1}]", Var1Index, Var2Index);
        }
    }

    public class GDLQuery_EqualVarSym
    {
        public int VarIndex = 0;
        public string Symbol;
        public short SymbolValue;
        public GDLQuery_EqualVarSym(int vIndex, string symbol)
        {
            VarIndex = vIndex;
            Symbol = symbol;
            SymbolValue = Translator.Instance.ToValue(symbol);
        }

        public override string ToString()
        {
            return string.Format("[{0}]={1}", VarIndex, Symbol);
        }
    }
}
