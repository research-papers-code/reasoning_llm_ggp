using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class CompilerContext
    {
        public bool NextInitBranch = false;
        public int Depth = 0;
        public int RecurrenceStartDepth = 0;
        public List<GDLRule> Rules = new List<GDLRule>();
        public string RecurrenceRule = null;
        public bool Recurrence = false;


        Dictionary<GDLRule, int> Counter = null;
        public void StartRecurrence()
        {
            Recurrence = true;
            Counter = new Dictionary<GDLRule, int>();
        }

        public int ConsumeRecurrentRule(GDLRule rule)
        {
            if (Counter.ContainsKey(rule))
            {
                Counter[rule]++;
                return Counter[rule];
            }
            else
            {
                Counter.Add(rule, 1);
                return 1;
            }
        }
        public CompilerContext(bool nextInitBranch)
        {
            NextInitBranch = nextInitBranch;
        }

        public bool NextInitLevel
        {
            get { return NextInitBranch && Depth == 0; }
        }
        public CompilerContext Clone()
        {
            CompilerContext clone = new CompilerContext(NextInitBranch);
            clone.Depth = Depth;
            clone.Rules.AddRange(Rules);
            clone.Recurrence = Recurrence;
            if (Recurrence)
            {
                clone.Counter = new Dictionary<GDLRule, int>(Counter);
            
                clone.RecurrenceRule = RecurrenceRule;
                clone.RecurrenceStartDepth = RecurrenceStartDepth;
            }
            return clone;
        }
    }
}
