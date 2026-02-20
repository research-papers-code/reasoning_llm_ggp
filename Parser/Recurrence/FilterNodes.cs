using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class FilterNodes
    {
        public FilterCollection Filter;
        public List<NodeOR> Nodes;

        public FilterNodes(FilterCollection filter, NodeOR initialNode)
        {
            this.Filter = filter;
            Nodes = new List<NodeOR>() { initialNode};
        }

        public NodeOR Pop()
        {
            if (Nodes.Count > 0)
            {
                NodeOR or = Nodes.Last();
                Nodes.RemoveAt(Nodes.Count - 1);
                return or;
            }
            return null;
        }

        public void Push(NodeOR node)
        {
            Nodes.Add(node);
        }
    }
}
