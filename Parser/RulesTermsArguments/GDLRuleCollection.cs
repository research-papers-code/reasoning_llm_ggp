using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class GDLRuleCollection
    {
        public List<GDLRule> Rules = new List<GDLRule>();
 
        public void AddRule(GDLRule rule)
        {
            Rules.Add(rule);
        }

        public override string ToString()
        {
            return Rules.Count + " rules";
        }
    }
}
