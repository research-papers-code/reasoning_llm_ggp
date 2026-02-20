using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class RecurrenceByCondition
    {
        List<FilterNodes> temp;
        public Dictionary<int, List<FilterNodes>> Nodes = new Dictionary<int, List<FilterNodes>>();

        public NodeOR Find(FilterCollection filters, int filtersHashCode)
        {
            if (filtersHashCode < 0)
                throw new Exception("impossible");

            if (Nodes.TryGetValue(filtersHashCode, out temp))
            {
                foreach (FilterNodes filterNodes in temp)
                {
                    if (filterNodes.Filter.Equals(filters))
                        return filterNodes.Pop();
                }
            }

            return null;
        }

        public void Push(NodeOR node, FilterCollection filters, int filtersHashCode)
        {
            if (filtersHashCode < 0)
                throw new Exception("impossible");

            if (Nodes.TryGetValue(filtersHashCode, out temp))
            {
                foreach (FilterNodes filterNodes in temp)
                {
                    if (filterNodes.Filter.Equals(filters))
                    {
                        filterNodes.Push(node);
                        return;
                    }
                }
                temp.Add(new FilterNodes(filters, node));
            }
            else
            {
                FilterNodes fn = new FilterNodes(filters, node);
                Nodes.Add(filtersHashCode, new List<FilterNodes>() { fn });
            }
        }
    }
}
