using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser.Simulator
{
    public class FrameFact
    {
        public NodeOR RuleNode;
        public NodeOR DataNode;

        public RowSet InitialData;

        public FrameFact(NodeOR ruleNode, NodeOR dataNode)
        {
            RuleNode = ruleNode;
            DataNode = dataNode;
            InitialData = new RowSet(dataNode.Data.Arity, dataNode.Data.Capacity);
            InitialData.Rewrite(dataNode.Data);
        }

        public override string ToString()
        {
            return RuleNode.Name + " " + DataNode.Data.ToString();
        }
    }
}
