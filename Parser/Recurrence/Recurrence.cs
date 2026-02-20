using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Parser
{
    public class Recurrence
    {
        public static int Expands = 0;
        public static bool Mode = false;
        public static int ID_SEED = 0;

        /*[START] characteristic data*/
        public int ID;
        public GDLTerm Condition;
        public FilterCollection FilterstoPass;
        public int HashCode = -1;
        public NodeAND NodeAND;
        /*[END] characteristic data*/
        
        public GDLCompiler Compiler;
        
        public static Stopwatch stoper = new Stopwatch();
        public Recurrence(GDLTerm gdlTerm, FilterCollection toPass, GDLCompiler compilerCreator, NodeAND nodeAND)
        {
            ID = ID_SEED++;
            Condition = gdlTerm;
            FilterstoPass = toPass;

            if (FilterstoPass != null && FilterstoPass.Count > 0)
                HashCode = FilterstoPass.ComputeHashCode();
            else
                HashCode = 0;
            Compiler = compilerCreator;
            NodeAND = nodeAND;

        }

        public void PushBack(NodeOR nodeOR)
        {
            stoper.Start();
            Compiler.RecurrenceMapByCondition[Condition.ConditionID].Push(nodeOR, FilterstoPass,HashCode);
            stoper.Stop();
        }

        public NodeOR Expand()
        {
           return Compiler.CreateOR(Condition.Name, Condition, FilterstoPass, NodeAND, false, true);
        }

        public NodeOR GetNode()
        {
           stoper.Start();
           NodeOR result = Compiler.RecurrenceMapByCondition[Condition.ConditionID].Find(FilterstoPass, HashCode);
           stoper.Stop();
           if (result != null)
           {
               result.Parent = NodeAND;
           }
           else
           {
               result = Expand();
           }
           result.CorrectFilters();
           return result;
        }
    }
}
