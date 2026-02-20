using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Parser.Constraints;

namespace Parser
{
    public partial class GDLCompiler
    {
        private HashSet<string> roleVars = new HashSet<string>();
        #region CreateStructure
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ruleName"></param>
        /// <param name="orNode"></param>
        /// <returns>0: constant, 1: frame, -1: no facts</returns>
        public int GetFacts(string ruleName, out NodeOR orNode, GDLTerm condition)
        {
            if (condition != null)
            {
                int arity = condition.Arguments.Count;
                if (ConstantFacts.TryGetValue(ruleName, out orNode))
                {
                    if (orNode.Arity == arity)
                        return 0;
                    else
                        orNode = null;
                }
                if (FrameFacts.TryGetValue(ruleName, out orNode))
                {
                    if (orNode.Arity == arity)
                        return 1;
                    else
                        orNode = null;
                }
            }
            else
            {
                if (ConstantFacts.TryGetValue(ruleName, out orNode))
                    return 0;
                   
                
                if (FrameFacts.TryGetValue(ruleName, out orNode))
                {
                   
                        return 1;
                 
                }
            }
            return -1;
        }

        public NodeOR CreateOR(string ruleName, GDLTerm condition, FilterCollection passedFilters, NodeAND parent, bool nextInitLevel, bool recurrence = false)
        {

            GDLRuleCollection realizations = null;
            if (condition == null)
                kb.NonFrameRules.TryGetValue(ruleName, out realizations);
            else
            {
                if (condition.HashID < 0)
                    UpdateConditionHash(condition);
                kb.ConditionRules.TryGetValue(condition.HashID, out realizations);
            }

            return CreateOR(ruleName, condition, passedFilters, realizations, parent, nextInitLevel, recurrence);
        }
        public NodeOR CreateOR(string ruleName, GDLTerm condition, FilterCollection passedFilters, GDLRuleCollection realizations, NodeAND parent, bool nextInitLevel, bool recurrence = false)
        {

            NodeOR groundData = null;
            GDLQuery query = null;
            int factType = -1;
            int arity = 0;

            if (CreatedRuleType == _Compilers.GDLCompilerRuleType.Terminal && ruleName.Equals("legal"))
            {
                simulator.TerminalContainsLegal = true;
            }
            if (ruleName.Equals("does"))
            {
                factType = 1;
                groundData = doesNode;
                arity = doesNode.Arity;

            }
            else
            {
                if (nextInitLevel == false)
                    factType = GetFacts(ruleName, out groundData, condition); //0: constant, 1: frame, -1: no facts<

                #region calculating arity

                if (groundData != null)
                    arity = Math.Max(arity, groundData.Arity);
                if (realizations != null)
                    arity = Math.Max(arity, realizations.Rules.First().Header.CurrentAriy);
                #endregion

            }
            NodeOR orNode = new NodeOR(ruleName, groundData, arity, parent);
            if (condition != null)
            {
                if (condition.Query == null)
                {
                    query = new GDLQuery(condition);
                }
                else
                {
                    query = condition.Query;
                }
                if (query.ConstraintsCount > 0 && groundData != null)
                    orNode.GroundQuery = query;

            }

            int filterCount = 0;
            if (passedFilters != null)
                filterCount = passedFilters.Count;

            /*realizations*/
            if (realizations != null)
            {
                orNode.Realizations = new List<NodeAND>();
                foreach (GDLRule rule in realizations.Rules)
                {
                    FilterCollection personalizedFilters = null;
                    if (filterCount > 0)
                        personalizedFilters = passedFilters.Clone();

                    NodeAND andNode = CreateAND(rule, personalizedFilters, orNode, nextInitLevel, recurrence);
                    orNode.Realizations.Add(andNode);
                    andNode.duplicateKiller = new DuplicateRemover(orNode.Arity);
                    if (orNode.Parent == null)
                        andNode.duplicateKiller.LIMIT = 100;
                }
            }
            if (factType == -1)
            {
                orNode.Mode = 4;
                orNode.Data = orNode.Realizations[0].MainData;
            }
            else
            {
                bool queryEnabled = (orNode.GroundQuery != null && orNode.GroundQuery.ConstraintsCount > 0);
                bool precomputed = (factType == 0) || (factType == 1 && !queryEnabled);

                if (factType == 0 && queryEnabled)
                {
                    NodeOR precomputedGround = new NodeOR(ruleName, null, groundData.Arity, parent);
                    precomputedGround.Data = new RowSet(groundData.Arity, groundData.Data.Capacity);
                    precomputedGround.Data.Rewrite(groundData.Data);

                    int rowPtr = 0;
                    for (int i = 0; i < precomputedGround.Data.Count; ++i)
                    {
                        if (orNode.GroundQuery.PassRow(precomputedGround.Data, rowPtr) == false)
                            precomputedGround.Data.MarkDeleted(i);
                        rowPtr += precomputedGround.Arity;
                    }
                    precomputedGround.Data.FinalizeDeletion();
                    precomputedGround.Data.PerformHash();
                    if (EnableCPP)
                        cpp.CreateOR(precomputedGround, InterpreterID);
                    orNode.Ground = precomputedGround;
                }
                if (precomputed && orNode.RealizationsCount == 0)
                {
                    orNode.Data = orNode.Ground.Data;
                    orNode.Mode = 0;
                }
                else if (!precomputed && orNode.RealizationsCount == 0)
                    orNode.Mode = 1;
                else if (precomputed && orNode.RealizationsCount > 0)
                    orNode.Mode = 2;
                else if (!precomputed && orNode.RealizationsCount > 0)
                    orNode.Mode = 3;
            }


            if (orNode.Data == null)
                orNode.Data = new RowSet(arity);

            if (!ReuseMgr.IsRegisteringFinished && nextInitLevel == false && orNode.Mode >= 2)
            {
                bool noQuery = (query == null || query.ConstraintsCount == 0);
                if (filterCount == 0 && noQuery) 
                {
                    /*producer*/
                    orNode.ReuseLink = new ReuseLink();
                    orNode.ReuseLink.ProducerConsumer = true;
                    ReuseMgr.RegisterSource(ruleName, orNode);
                    ReuseMgr.RegisterConsumer(ruleName, orNode);
                }
                else if (recurrence == false || filterCount == 0)
                {
                    /*consumer*/
                    orNode.ReuseLink = new ReuseLink();
                    if (filterCount > 0)
                        orNode.GroundFilter = passedFilters.CloneRuntime();

                    if (query != null && query.ConstraintsCount > 0)
                        orNode.GroundQuery = query;

                    orNode.ReuseLink.ProducerConsumer = false;
                    ReuseMgr.RegisterConsumer(ruleName, orNode);
                }
            } 
            return orNode;
        }
      

        public NodeAND CreateAND(GDLRule rule, FilterCollection passedFilters, NodeOR parent, bool nextInitLevel, bool recurrence)
        {
            if (passedFilters == null)
                passedFilters = new FilterCollection();

            passedFilters.ApplyPassedForRule(rule);
            NodeAND andNode = new NodeAND(rule, parent);
            andNode.TEMP_PF = passedFilters;
            int headerVarCount;
            int arity = rule.Header.CountDuplicatesEx(out headerVarCount) + rule.VariablesMapping.Count;
            andNode.Data = new RowSet[rule.PathsCount];
            for (int pathIndex = 0; pathIndex < rule.PathsCount; ++pathIndex)
            {
                andNode.Data[pathIndex] = new RowSet(arity);
            }
            andNode.MainData = andNode.Data[0];

            roleVars.Clear();
            foreach (VariablesOperation vo in rule.Operations)
            {
                switch (vo.Type)
                {
                    case VariablesOperationType.ForkInto:
                        foreach (VariablesOperation forkVo in vo.ForkedNext)
                        {
                            MergeOperation op = CreateOR_Operation(rule, passedFilters, andNode, forkVo, nextInitLevel);
                            if (op.PathIndex > 0)
                                op.ForkType = 1;
                            andNode.AddOperation(op);
                        }
                        break;
                    default:
                        andNode.AddOperation(CreateOR_Operation(rule, passedFilters, andNode, vo, nextInitLevel));
                        break;
                }
            }
            foreach (MergeOperation op in andNode.Operations)
            {
                if (op.Filtering != null && op.Filtering.Filters.Count == 0)
                    op.Filtering = null;
              
            }
            GDLQuery query = new GDLQuery(rule.Header);
            if (query.ConstraintsCount > 0)
            {
                /*Return query*/
                if (passedFilters.Count > 0 && recurrence)
                {
                    andNode.ReturnFilterQuery = passedFilters.ReduceByQuery(query);
                }
                andNode.ReturnQuery = query;
            }
            return andNode;
        }

        private void SetGroundFilter(NodeOR orNode, FilterCollection passedFilters)
        {
            if (passedFilters.Filters.Count > 0)
            {
                orNode.GroundFilter = new FilterCollection();
                foreach (Filter childFilter in passedFilters.Filters)
                    orNode.GroundFilter.AddFilter(childFilter);
            }
        }
        private MergeOperation CreateOR_Operation(GDLRule rule, FilterCollection passedFilters, NodeAND andNode, VariablesOperation vo, bool nextInitLevel)
        {

            MergeOperation operation = new MergeOperation();
            operation.duplicateKiller = new DuplicateRemover(andNode.MainData.Arity);

            operation.Condition = vo.RelatedCondition;
            operation.PathIndex = vo.PathIndex;
            operation.IntoData = andNode.Data[operation.PathIndex];
            operation.ForkIntoCount = vo.ForkIntoCount;

            int index = 0;

            operation.ForkBackColumns = new int[vo.TotalInitialized.Count];
            foreach (string var in vo.TotalInitialized)
                operation.ForkBackColumns[index++] = vo.VariablesMapping[var];
            if (vo.Type == VariablesOperationType.ForkReturn)
            {
                operation.ForkType = 2;
                return operation;
            }
            operation.ForkType = 0;

            if (vo.RelatedCondition.HashID < 0)
                UpdateConditionHash(vo.RelatedCondition);

            bool hasRules = kb.ConditionRules.ContainsKey(vo.RelatedCondition.HashID);
            FilterCollection toPass = null;
            FilterCollection toApply = null;

            if (vo.NewVariables.Count > 0)
                operation.NewMapping = new GDLMapping(vo.NewVariables.Count);
            if (vo.CommonVariables.Count > 0)
                operation.CommonMapping = new GDLMapping(vo.CommonVariables.Count);

            /*MAPPING*/
            index = 0;
            if (vo.Type == VariablesOperationType.Regular)
            {
                foreach (string varName in vo.NewVariables)
                {
                    operation.NewMapping.LocalColumns[index] = rule.VariablesMapping[varName];
                    operation.NewMapping.ForeignColumns[index] = vo.RelatedCondition.FirstArgumentIndexOf(varName);
                    ++index;
                }
                index = 0;
                foreach (string varName in vo.CommonVariables)
                {
                    operation.CommonMapping.LocalColumns[index] = rule.VariablesMapping[varName];
                    operation.CommonMapping.ForeignColumns[index] = vo.RelatedCondition.FirstArgumentIndexOf(varName);
                    ++index;
                }

            }
            passedFilters.ApplyPassedForCondition(rule, andNode, true, vo, operation, out toPass, out toApply, roleVars);
            foreach (Filter f in toPass.Filters)
            {
                if (f.Source == null)
                {
                    f.Source = operation.IntoData;
                    f.SourcePathIndex = operation.PathIndex;
                }
            }
            operation.Filtering = toApply;
            bool continueProcess = true;
            if (vo.RelatedCondition.Name.Equals(rule.Header.Name) && nextInitLevel == false)
            {
                operation.Recurr = new Recurrence(vo.RelatedCondition, toPass.Clone(), this, andNode);
                if (RecurrenceMapByCondition.ContainsKey(operation.Condition.ConditionID) == false)
                    RecurrenceMapByCondition.Add(operation.Condition.ConditionID, new RecurrenceByCondition());

                RecurrenceMapByID.Add(operation.Recurr.ID, operation.Recurr);
                continueProcess = false;
                NodeOR p = andNode.TryGoUpOR(1); 
                if (p != null)
                {
                    continueProcess = (p.Name.Equals(vo.RelatedCondition.Name) == false);
                }
            }
            if (continueProcess)
            {
                operation.FromNode = CreateOR(vo.RelatedCondition.Name, vo.RelatedCondition, toPass, andNode, operation.Recurr != null);
            }

			//----------------------------------------- GROUND FILTER --------------------------------------------------//
			if (hasRules == false && toPass != null && toPass.Count > 0)
			{
				if (operation.FromNode.Mode == 0)
				{
					operation.FromNode.Data = new RowSet(operation.FromNode.Ground.Arity);

				}

				operation.FromNode.Mode = -1;
				SetGroundFilter(operation.FromNode, toPass);
			}
            operation.MergeType = vo.MergeOperationType;
            if (operation.MergeType < 4 && (toApply.Count > 0 || vo.DistinctConditionsApplied.Count > 0)) //Filtered
                operation.MergeType += 8; 


            if (vo.DistinctConditionsApplied.Count > 0)
            {
                operation.DistinctQuery = new GDLQuery();
                operation.DistinctQuery.EqualSymbol = new List<GDLQuery_EqualVarSym>();
                operation.DistinctQuery.EqualVariable = new List<GDLQuery_EqualVarVar>();
                foreach (GDLTerm dt in vo.DistinctConditionsApplied)
                {
                    if (dt.Arguments[0].Constant && dt.Arguments[1].Constant == false)
                    {
                        int column1 = rule.VariablesMapping[dt.Arguments[1].Name];
                        string symbol = dt.Arguments[0].Name;

                        operation.DistinctQuery.EqualSymbol.Add(new GDLQuery_EqualVarSym(column1, symbol));
                    }
                    if (dt.Arguments[0].Constant == false && dt.Arguments[1].Constant)
                    {
                        int column1 = rule.VariablesMapping[dt.Arguments[0].Name];
                        string symbol = dt.Arguments[1].Name;
                        operation.DistinctQuery.EqualSymbol.Add(new GDLQuery_EqualVarSym(column1, symbol));
                    }
                    if (dt.Arguments[0].Constant == false && dt.Arguments[1].Constant == false)
                    {
                        int column1 = rule.VariablesMapping[dt.Arguments[0].Name];
                        int column2 = rule.VariablesMapping[dt.Arguments[1].Name];
                        operation.DistinctQuery.EqualVariable.Add(new GDLQuery_EqualVarVar(column1, column2));
                    }
                }
            }
            return operation;
        }
        #endregion
    }
}
