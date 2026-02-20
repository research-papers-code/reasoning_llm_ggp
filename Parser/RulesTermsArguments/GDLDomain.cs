using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class GDLArgumentDomain
    {
        public Dictionary<string, List<int>> RelationColumns = new Dictionary<string, List<int>>();
        public int Arity = 1;
        List<int> temp = null;

        public int ID = 0;
        public static int ID_SEED = 0;
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var entry in RelationColumns)
            {
                foreach (int index in entry.Value)
                {
                    builder.AppendFormat("({0}[{1}])", entry.Key, index);
                }
            }
            return builder.ToString();
        }

        public GDLArgumentDomain(string relationName, int columnIndex)
        {
            ID = ID_SEED++;
            RelationColumns.Add(relationName, new List<int>() { columnIndex });
        }

        public bool Contains(string relationName, int columnIndex)
        {
            if (RelationColumns.TryGetValue(relationName, out temp))
                return temp.Contains(columnIndex);
            return false;
        }

        public void Add(string relationName, int columnIndex)
        {
            if (RelationColumns.TryGetValue(relationName, out temp))
            {
                if (temp.Contains(columnIndex) == false)
                    temp.Add(columnIndex);
            }
            else
            {
                RelationColumns.Add(relationName, new List<int>() { columnIndex });
            }
        }

        public void Join(GDLArgumentDomain other)
        {
            if (other.Arity > Arity)
                Arity = other.Arity;
            foreach (var entry in other.RelationColumns)
            {
                if (RelationColumns.TryGetValue(entry.Key, out temp))
                {
                    foreach (int i in entry.Value)
                    {
                        if (temp.Contains(i) == false)
                            temp.Add(i);
                    }
                }
                else
                {
                    RelationColumns.Add(entry.Key, entry.Value);
                }
            }
        }
    }
}
