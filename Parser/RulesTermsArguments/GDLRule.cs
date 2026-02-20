using System;
using System.Collections.Generic;
using System.Linq;

namespace Parser
{
    public class GDLRule
    {
        public bool VariableStateCalculated = false;
        public GDLTerm Header;
        public List<GDLTerm> Conditions = new List<GDLTerm>();

        public Dictionary<string, GDLArgumentDomain> DomainMapping = new Dictionary<string, GDLArgumentDomain>();
       
        public bool ContainedORDistinctInitally = false;
        public bool ContainedNOTDistinctInitially = false;
        public bool ContainsOr
        {
            get { return Conditions.FirstOrDefault(a => a.OR == true) != null; }
        }
        private bool Unsafe = false;
        
        public HashSet<string> UnsafeVariables = new HashSet<string>();
        
        public Dictionary<string, int> VariablesMapping = null;
        public List<VariablesOperation> Operations = new List<VariablesOperation>();
        public List<GDLTerm> DistinctConditions = new List<GDLTerm>();
        public int PathsCount = 0;

        public int OrCount = 0;
        public static int RuleID_SEED = 0;
        public GDLRule()
        {
           
        }

        public override string ToString()
        {
            string txt = Header.ToString() + "\n";
            foreach (GDLTerm term in Conditions)
                txt += term.ToString();

            
            return txt;
             
        }


        public void TryAddingSplitPaths(List<VariablesOperation> totalOperations, List<VariablesOperation> splitOperations)
        {
            if (splitOperations.Count < 2)
                return;

            VariablesOperation first = splitOperations.First(a => a.PathIndex == 0);
            bool change = true;

            while (change)
            {
                change = false;
                foreach (VariablesOperation other in splitOperations)
                {
                    if (other != first)
                    {
                        if (VariablesOperation.EqualInitializationState(other, first))
                        {
                            VariablesOperation forkAdd = new VariablesOperation(other.VariablesMapping, null, other.DistinctConditionsLeft, other.PathIndex,0);
                            forkAdd.Comment = "FORK->RETURN";
                            forkAdd.TotalInitialized = new HashSet<string>(other.TotalInitialized);
                            forkAdd.Type = VariablesOperationType.ForkReturn;
                            forkAdd.RelatedCondition = other.RelatedCondition;
                            Operations.Add(forkAdd);
                            splitOperations.Remove(other);
                            change = true;
                            break;
                        }
                    }
                }
            }
        }

        private void MapVariables()
        {
            DomainMapping.Clear();
            DomainMapping = null;
            UnsafeVariables = null;
            VariablesMapping = new Dictionary<string, int>();

            for (int i = 0; i < Header.Arguments.Count; ++i)
            {
                GDLArgument arg = Header.Arguments[i];
                if (VariablesMapping.ContainsKey(arg.Name) == false)
                    VariablesMapping.Add(arg.Name, i);
            }
            HashSet<string> allVariables = new HashSet<string>();
            HashSet<string> conditionVariables = new HashSet<string>();

            int varIndexCounter = Header.Arguments.Count;
            foreach (GDLTerm condtion in Conditions)
            {
                conditionVariables.Clear();
                condtion.ExtractVariables(conditionVariables);
                foreach (string var in conditionVariables)
                {
                    if (allVariables.Contains(var) && VariablesMapping.ContainsKey(var) == false)
                        VariablesMapping.Add(var, varIndexCounter++);
                }

                foreach (string var in conditionVariables)
                    allVariables.Add(var);
            }
        }

        public void CalculateVariablesState()
        {
            if (VariableStateCalculated)
                return;
            VariableStateCalculated = true;
            MapVariables();
            foreach (GDLTerm distinctCondition in Conditions)
            {
                if (distinctCondition.DistinctKeyword)
                    DistinctConditions.Add(distinctCondition);
            }
            if (DistinctConditions.Count > 0)
            {
                foreach (GDLTerm distinctCondition in DistinctConditions)
                    Conditions.Remove(distinctCondition);
            }

            PathsCount = 1;
            VariablesOperation FirstState = new VariablesOperation(VariablesMapping, null, DistinctConditions, 0,0);

            List<VariablesOperation> lastActiveOperations = new List<VariablesOperation>() { FirstState };
            List<VariablesOperation> newActiveOperations = new List<VariablesOperation>();


            if (Conditions.Count > 0)
                Operations.Add(FirstState);

            foreach (GDLTerm condition in Conditions)
            {
                newActiveOperations.Clear();
                foreach (VariablesOperation op in lastActiveOperations)
                {
                    op.Consume(newActiveOperations, condition, op.PathIndex, this);
                   
                }
                TryAddingSplitPaths(Operations, newActiveOperations);
                lastActiveOperations.Clear();
                lastActiveOperations.AddRange(newActiveOperations);
                Operations.AddRange(newActiveOperations);
            }
            if (newActiveOperations.Count > 1)
                TryAddingSplitPaths(Operations, newActiveOperations);
            if (newActiveOperations.Count > 0)
            {
                foreach (VariablesOperation op in newActiveOperations)
                    Operations.Remove(op);
            }
        }

        public void AddCondition(GDLTerm term)
        {
            if (term.NOT && term.DistinctKeyword) /*not distinct*/
            {
                ContainedNOTDistinctInitially = true;
            }
            if (term.OR)
            {
                if(term.OrTerms.FirstOrDefault(a=> a.Name.Equals("distinct")) != null)
                {
                    ContainedORDistinctInitally = true;
                }
                ++OrCount;
            }
            GDLTerm notTerm = term.FirstNegatedOrNULL();
            if (notTerm != null)
            {
                HashSet<string> variables = new HashSet<string>();
                foreach (GDLTerm addedTerm in Conditions)
                    addedTerm.ExtractVariables(variables);

                foreach (GDLArgument arg in notTerm.Arguments)
                    arg.ExtractNewVariables(UnsafeVariables, variables);

                Unsafe = (UnsafeVariables.Count > 0);
            }
            if (Unsafe == false || notTerm != null)
                Conditions.Add(term);
            else
            {
                HashSet<string> newVariables = new HashSet<string>();
                term.ExtractVariables(newVariables);
                foreach (string varName in newVariables)
                    UnsafeVariables.Remove(varName);

                if (Conditions.Count > 0)
                    Conditions.Insert(Conditions.Count - 1, term);
                Unsafe = (UnsafeVariables.Count > 0);
            }
        
        }

        public GDLRule Clone()
        {
            GDLRule clone = new GDLRule();
            clone.Header = this.Header.Clone();
            foreach (GDLTerm condition in Conditions)
                clone.AddCondition(condition.Clone());

            return clone;
            
        }

        public void ReplaceArgument(string oldName, string newName,List<GDLTerm> newConditions)
        {
            foreach (GDLArgument arg in Header.Arguments)
            {
                if (arg.Name == oldName)
                {
                    arg.SetName(newName);
                    arg.Constant = newName[0] != '?';
                }
            }
            foreach (GDLTerm condition in Conditions)
                condition.ReplaceArgument(oldName, newName,newConditions);
        }
    }
}
