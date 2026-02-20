using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Parser.Simulator;

namespace Parser
{
    public class NodeAND
    {  
        public int ID = -1;
        public int PathsCount = 1;
        public int ActivePaths;
        public DuplicateRemover duplicateKiller = null;

        public RowSet MainData = null;
        public RowSet[] Data = null;
        public List<MergeOperation> Operations = new List<MergeOperation>();

        public GDLQuery ReturnQuery;
        public List<FilterQuery> ReturnFilterQuery;
      
        internal string Name { get; set; }
        public int NameValue { get; set; }
        public GDLRule Rule;
        public static int ID_SEED = 0;
        public NodeOR Parent = null;

        public string RuleName;
        public FilterCollection TEMP_PF;
        public NodeAND(GDLRule rule, NodeOR parent)
        {
            RuleName = rule + "";
            ID = ID_SEED++;
            Rule = rule;
            PathsCount = rule.PathsCount;
            Parent = parent;
            Name = Rule.Header.Name;
        }

        /*---------------------------------------------------------------------------------------------------------------------------------------*/
        public void AddOperation(MergeOperation operation)
        {
            Operations.Add(operation);
        }

        public override string ToString()
        {
            return Rule.ToString();
        }


        public bool Prove()
        {
            if (ReturnFilterQuery != null)
            {
                foreach (FilterQuery fq in ReturnFilterQuery)
                {
                    if (fq.Pass() == false)
                        return false;
                }
            }
            MainData.Clear();
            for (int i = 0; i < PathsCount; ++i)
                Data[i].ActivePath = true;

            ActivePaths = 1;
           
            foreach (MergeOperation operation in Operations)
            {
                if (operation.IntoData.ActivePath)
                {
                    if (operation.ForkIntoCount > 0)
                        PrepareFork(operation);

                    bool operationResult = ProveOperation(operation);
                    if (operationResult == false)
                    {
                        operation.IntoData.Clear(); 
                        --ActivePaths;
                        operation.IntoData.ActivePath = false;
                        if (ActivePaths == 0)
                            return false;
                    }
                }
            }
            if (ReturnQuery != null)
            {
                if (MainData.Count == 0)
                    MainData.Count = 1;
                
                int rowPtr = 0;
                for (int i = 0; i < MainData.Count; ++i)
                {
                    ReturnQuery.FillRow(MainData, rowPtr);
                    rowPtr += MainData.Arity;
                }
            }
            return true;
        }

        private void PrepareFork(MergeOperation operation)
        {
            ActivePaths += operation.ForkIntoCount;
            for (int path = 1; path < PathsCount; ++path)
            {
                Data[path].Clear();
                if (operation.ForkBackColumns.Length != MainData.Arity)
                    Data[path].MergeForBack(MainData, operation.ForkBackColumns);
                else
                    Data[path].Rewrite(MainData);
            }
        }
        private bool ProveOperation(MergeOperation operation)
        {
            int oldCount;
            if (operation.ForkType == 2)
            {
                --ActivePaths;
                oldCount = MainData.Count;
                MainData.MergeForBack(operation.IntoData, operation.ForkBackColumns);
                if (Data[0].ActivePath == false)
                {
                    Data[0].ActivePath = true;
                    ++ActivePaths;
                }
               
                operation.duplicateKiller.Run(MainData, operation.ForkBackColumns, oldCount);
                return true;
            }

            if (operation.Recurr != null)
            {
                operation.FromNode = operation.Recurr.GetNode();          
            }
            bool result = true;
            bool operationResult = operation.FromNode.Prove();
            if (operationResult == false && operation.MergeType != 4 && operation.MergeType != 21)
            {
                if (operation.Recurr != null)
                {
                    operation.Recurr.PushBack(operation.FromNode);
                }
                return false;
            }
 
            operation.IntoData.Filtering = operation.Filtering;
            operation.IntoData.DistinctQuery = operation.DistinctQuery;

            oldCount = operation.IntoData.Count;

            switch (operation.MergeType)
            {
                case 0:
                    if (!operation.IntoData.MergeNew(operation.NewMapping, operation.FromNode.Data))
                        result = false;
                    break;
                case 1:
                    if (!operation.IntoData.MergeTrue(operation.NewMapping, operation.CommonMapping, operation.FromNode.Data))
                        result = false;
                    break;
                case 2:
                    if (!operation.IntoData.MergeCombine(operation.NewMapping, operation.FromNode.Data))
                        result = false;
                    break;
                case 3:
                    if (!operation.IntoData.MergeVerify(operation.CommonMapping, operation.FromNode.Data))
                        result = false;
                    break;
                case 4:
                    if (!operation.IntoData.MergeNOT(operation.CommonMapping, operation.FromNode.Data, operationResult))
                        result = false;
                    break;
                /*############ Query AND Filtering */
                case 5:
                    if (!operation.IntoData.MergeNew_Q(operation.NewMapping, operation.FromNode.Data))
                        result = false;
                    break;
                case 6:
                    if (!operation.IntoData.MergeTrue_Q(operation.NewMapping, operation.CommonMapping, operation.FromNode.Data))
                        result = false;
                    break;
                case 7:
                    if (!operation.IntoData.MergeCombine_Q(operation.NewMapping, operation.FromNode.Data))
                        result = false;
                    break;
                /*############ Query or Filtering -> testing */
                case 8:
                    if (!operation.IntoData.MergeNew_FQL(operation.NewMapping, operation.FromNode.Data))
                        result = false;
                    else
                        operation.duplicateKiller.Run(operation.IntoData, operation.ForkBackColumns, 0);
                    operation.StopFiltering();
                    break;
                case 9:
                    if (!operation.IntoData.MergeTrue_FQL(operation.NewMapping, operation.CommonMapping, operation.FromNode.Data))
                        result = false;
                    else
                        operation.duplicateKiller.Run(operation.IntoData, operation.ForkBackColumns, 0);
                    operation.StopFiltering();
                 
                    break;
                case 10:
                    if (!operation.IntoData.MergeCombine_FQL(operation.NewMapping, operation.FromNode.Data))
                        result = false;
                    else
                        operation.duplicateKiller.Run(operation.IntoData, operation.ForkBackColumns, 0);
                    operation.StopFiltering();
                    break;
                default:
                    break;
            }    
			
            if (result == true)
            {
                int lowest = operation.IntoData.FinalizeDeletion(operation);
                if (oldCount > 0)
                {
                    if (operation.MergeType == 1 || operation.MergeType == 6)
                    {
                        oldCount = Math.Min(oldCount, lowest);
                        operation.duplicateKiller.Run(operation.IntoData, operation.ForkBackColumns,lowest);
                    }
                }
                if (operation.Filtering != null)
                {
                    if (operation.MergeType < 8)
                        result = operation.IntoData.FilterOut();
                }

            }
            if (operation.Recurr != null)
                operation.Recurr.PushBack(operation.FromNode);
            return result;
        }

        public NodeOR TryGoUpOR(int distance)
        {
            NodeOR current = Parent;
            for (int i = 1; i < distance; ++i)
            {
                if(current != null && current.Parent != null)
                    current = current.Parent.Parent;
            }
            return current;
        }
        public NodeAND GoUpAND(int distance)
        {
            NodeAND current = Parent.Parent;
            for (int i = 1; i < distance; ++i)
            {
                current = current.Parent.Parent;
            }
            return current;
        }

        public void CorrectFilters()
        {
           
            foreach (MergeOperation op in Operations)
            {
				if (op.Filtering != null)
				{
					foreach (Filter f in op.Filtering.Filters)
					{
						f.Source = GoUpAND(f.Distance).Data[f.SourcePathIndex];

					}
				}
                if (op.FromNode != null && op.Recurr == null)
                    op.FromNode.CorrectFilters();
            }

            if (ReturnFilterQuery != null)
            {
                foreach (FilterQuery fq in ReturnFilterQuery)
                {
                    fq.Filter.Source = GoUpAND(fq.Filter.Distance).Data[fq.Filter.SourcePathIndex];

                }
            }
        }
    }
}
