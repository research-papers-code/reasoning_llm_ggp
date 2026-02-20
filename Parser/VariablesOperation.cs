using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class VariablesOperation
    {
        /*----------------------------------------------------------------*/
        public VariablesOperationType Type = VariablesOperationType.Regular;
        public int MergeOperationType = -1;
        public List<GDLTerm> DistinctConditionsApplied = new List<GDLTerm>();
        public int PathIndex;
        public int ForkIntoCount;
        public HashSet<string> NewVariables;
        public HashSet<string> CommonVariables;
        public Dictionary<string, int> VariablesMapping;
        public List<GDLTerm> DistinctConditionsLeft = null;
        /*-----------------------------------------------------------------*/
        public HashSet<string> TotalInitialized;
        
        /*Fork only*/
        public List<VariablesOperation> ForkedNext;
        public GDLTerm RelatedCondition = null;
        public string Comment = "";
        /*---------*/
     
        public override string ToString()
        {
            string txt = "";
            if (ForkedNext == null)
            {
                string nTxt = "";
                if (DistinctConditionsApplied.Count > 0)
                    nTxt = "There are distincts!";
                txt = string.Format("{0} N=[{1}] C=[{2}]   Path:{3}]", Comment, string.Join(" ", NewVariables), string.Join(" ", CommonVariables), PathIndex) + nTxt;
            }
            else
            {
                txt = "FORK";
            }
            return txt;
        }

        public bool Fork
        {
            get { return ForkedNext != null; }
        }

        public VariablesOperation(Dictionary<string, int> variablesMapping, HashSet<string> totalInitialized, List<GDLTerm> distinctConditionsLeft, int pathIndex, int forkintoCount)
        {
            this.PathIndex = pathIndex;
            NewVariables = new HashSet<string>();
            CommonVariables = new HashSet<string>();

            if (totalInitialized != null)
                TotalInitialized = new HashSet<string>(totalInitialized);
            else
                TotalInitialized = new HashSet<string>();

            if (distinctConditionsLeft != null && distinctConditionsLeft.Count > 0)
                DistinctConditionsLeft = new List<GDLTerm>(distinctConditionsLeft);
            
            ForkedNext = null;
            VariablesMapping = variablesMapping;
        }

        public void Consume(List<VariablesOperation> operations, GDLTerm currentCondition, int pathIndex, GDLRule rule)
        {
            if (currentCondition.OrTerms == null)
            {
                this.Comment = currentCondition.ToString();
                this.RelatedCondition = currentCondition;

                int totalInitializedBefore = TotalInitialized.Count;
                foreach (GDLArgument arg in currentCondition.Arguments)
                {
                    if (VariablesMapping.ContainsKey(arg.Name) && arg.Constant==false)
                    {
                        if (TotalInitialized.Contains(arg.Name))
                            CommonVariables.Add(arg.Name);
                        else
                        {
                            TotalInitialized.Add(arg.Name);
                            NewVariables.Add(arg.Name);
                        }
                    }
                }
                
                if (RelatedCondition.NOT)
                    MergeOperationType = 4; //MergeNOT
                else
                {
                    if (NewVariables.Count > 0)
                    {
                        if (CommonVariables.Count > 0)
                        {
                            MergeOperationType = 1; //MergeTrue
                        }
                        else /*common = 0*/
                        {
                            if (totalInitializedBefore == 0)
                            {
                                MergeOperationType = 0; //MergeNew
                            }
                            else
                            {
                                MergeOperationType = 2; //MergeCombine
                            }
                        }
                    }
                    else /*vo.NewVariables.Count ==0*/
                    {
                        MergeOperationType = 3; //MergeVerify
                    }
                }

                foreach (string var in NewVariables)
                    TotalInitialized.Add(var);

                if (DistinctConditionsLeft != null && DistinctConditionsLeft.Count > 0)
                {
                    foreach (GDLTerm distinctTerm in DistinctConditionsLeft)
                    {
                        if ((TotalInitialized.Contains(distinctTerm.Arguments[0].Name) || distinctTerm.Arguments[0].Constant)
                         && (TotalInitialized.Contains(distinctTerm.Arguments[1].Name) || distinctTerm.Arguments[1].Constant))
                            DistinctConditionsApplied.Add(distinctTerm);
                    }
                    foreach (GDLTerm distinctTerm in DistinctConditionsApplied)
                        DistinctConditionsLeft.Remove(distinctTerm); 
                }

                VariablesOperation nextOperation = new VariablesOperation(VariablesMapping, TotalInitialized, DistinctConditionsLeft, pathIndex, ForkIntoCount);
                nextOperation.RelatedCondition = currentCondition;
                operations.Add(nextOperation);
            }
            else
            {
                Type = VariablesOperationType.ForkInto;
                this.Comment = "FORK->" + currentCondition.ToString();
                this.RelatedCondition = currentCondition;
                ForkedNext = new List<VariablesOperation>();
                for (int i = 0; i < currentCondition.OrTerms.Count; ++i)
                {
                    int path = pathIndex;
                    if (i > 0)
                        path = rule.PathsCount++;

                    int forkIntoCount = 0;
                    if (i == 0)
                        forkIntoCount = currentCondition.OrTerms.Count-1;
                    VariablesOperation forkOperation = new VariablesOperation(VariablesMapping, TotalInitialized, DistinctConditionsLeft, path, forkIntoCount);
                    forkOperation.Consume(operations, currentCondition.OrTerms[i], path, rule);
                    forkOperation.ForkIntoCount = forkIntoCount;
                    ForkedNext.Add(forkOperation);
                }

            }
        }

        public static bool EqualInitializationState(VariablesOperation o1, VariablesOperation o2)
        {
            return o1.TotalInitialized.Count == o2.TotalInitialized.Count;
        }
    }
}
