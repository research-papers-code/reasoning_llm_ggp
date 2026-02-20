using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class FilterQuery
    {
        public GDLQuery Query = null;
        public Filter Filter = null;

        public FilterQuery(Filter filter, GDLQuery query)
        {
            Query = new GDLQuery();
            Filter = filter;

            foreach (var ev in query.EqualVariable)
            {
                int v1 = Filter.GetSourceVarIndex(ev.Var1Index);
                int v2 = Filter.GetSourceVarIndex(ev.Var2Index);
                if (v1 >= 0 && v2 >= 0)
                {
                    Query.EqualVariable.Add(new GDLQuery_EqualVarVar(v1, v2));
                }
            }
            foreach (var es in query.EqualSymbol)
            {
                int v = Filter.GetSourceVarIndex(es.VarIndex);
                if (v >= 0)
                {
                    Query.EqualSymbol.Add(new GDLQuery_EqualVarSym(v, es.Symbol));
                }
            }
        }

        public override string ToString()
        {
            return Filter.ToString() + "---" + Query.ToString();
        }

        public bool Empty
        {
            get
            {
                return Query.ConstraintsCount == 0;
            }
        }

        public bool Pass()
        {
            int rowPtr = 0;
            for (int i = 0; i < Filter.Source.Count; ++i)
            {
                if (Query.PassRow(Filter.Source, rowPtr))
                    return true;

                rowPtr += Filter.Source.Arity;
            }
            return false;
        }
    }
}
