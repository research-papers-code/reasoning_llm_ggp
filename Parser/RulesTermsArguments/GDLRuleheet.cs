using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class GDLRuleheet
    {
        public int PlayersCount = 2; 
        public List<GDLTerm> AllConditions = new List<GDLTerm>();
        public Dictionary<string, int> ConditionTextToID = new Dictionary<string, int>();

        public Dictionary<int, GDLRuleCollection> ConditionRules = new Dictionary<int, GDLRuleCollection>();
        Dictionary<string, GDLTermCollection> AllTerms = new Dictionary<string, GDLTermCollection>();


        public List<GDLRule> AllRules = new List<GDLRule>();
       
        public Dictionary<string, GDLRuleCollection> NonFrameRules = new Dictionary<string, GDLRuleCollection>();
        public Dictionary<string, GDLRuleCollection> NextRules = new Dictionary<string, GDLRuleCollection>();
        public Dictionary<string, GDLRuleCollection> InitRules = new Dictionary<string, GDLRuleCollection>();

        public Dictionary<string, GDLTermCollection> InitFacts = new Dictionary<string, GDLTermCollection>();
        public Dictionary<string, GDLTermCollection> ConstantFacts = new Dictionary<string, GDLTermCollection>();

        public List<string> RoleNames = new List<string>();
       
        public List<GDLArgumentDomain> ArgumentDomains = new List<GDLArgumentDomain>();

        public List<GDLTerm> LegalConditions = new List<GDLTerm>();
        public List<GDLTerm> GoalConditions = new List<GDLTerm>();

        #region CALCULATE-UPDATE-ARITY
        public int Arity_Legal;
        private string blankSymbolName;
        public int GetNamedArity(string relationName)
        {
            GDLRuleCollection ruleCollection = null;
            GDLTermCollection termCollection = null;
            int arity = 0;
            if (NonFrameRules.TryGetValue(relationName, out ruleCollection))
                arity = ruleCollection.Rules.First().Header.CurrentAriy;

            if (ConstantFacts.TryGetValue(relationName, out termCollection))
            {
                arity = Math.Max(arity, termCollection.Terms.First().CurrentAriy);
            }
            return arity;
        }

        public void CalculateArity()
        {
            foreach (var entry in NonFrameRules)
            {
                foreach (GDLRule rule in entry.Value.Rules)
                {
                    CalculateArity(rule);
                }
            }
            foreach (var entry in NextRules)
            {
                foreach (GDLRule rule in entry.Value.Rules)
                {
                    CalculateArity(rule); 
                }
            }
            foreach (var entry in InitRules)
            {
                foreach (GDLRule rule in entry.Value.Rules)
                {
                    CalculateArity(rule);
                }
            }
            foreach (var entry in InitFacts)
            {
                foreach (GDLTerm fact in entry.Value.Terms)
                {
                    CalculateArity(fact);
                }
            }
            foreach (var entry in ConstantFacts)
            {
                foreach (GDLTerm fact in entry.Value.Terms)
                {
                    CalculateArity(fact);
                }
            }

        }

        public void CalculateArity(GDLRule rule)
        {
            AddToTermCollection(rule.Header, AllTerms);
            for (int i = 0; i < rule.Header.Arguments.Count; ++i)
                MapRuleVariable(rule, rule.Header.Arguments[i], rule.Header.Name, i);

            
            foreach (GDLTerm condition in rule.Conditions)
            {
                if (condition.OR == false && condition.OrTerms == null)
                {
                    AddToTermCollection(condition, AllTerms);
                    for (int j = 0; j < condition.Arguments.Count; ++j)
                        MapRuleVariable(rule, condition.Arguments[j], condition.Name, j);
                }
                else
                {
                    foreach (GDLTerm childCondition in condition.OrTerms)
                    {
                        AddToTermCollection(childCondition, AllTerms);
                        for (int j = 0; j < childCondition.Arguments.Count; ++j)
                            MapRuleVariable(rule, childCondition.Arguments[j], childCondition.Name, j);
                    }
                }
            }
        }

        private void UpdateArity()
        {
            blankSymbolName = Translator.BlankSymbol;
            foreach (GDLArgumentDomain domain in ArgumentDomains)
            {
                if (domain.Arity > 1)
                {
                    foreach (var relationColumn in domain.RelationColumns)
                    {
                        GDLTermCollection relationTerms = null;
                        if (AllTerms.TryGetValue(relationColumn.Key, out relationTerms))
                        {
                            foreach (GDLTerm term in relationTerms.Terms)
                            {
                                foreach (int index in relationColumn.Value)
                                    UpdateArity(term, index, domain.Arity);
                            }
                        }
                        if (relationColumn.Key.Equals("legal"))
                        {
                            if (AllTerms.TryGetValue("does", out relationTerms))
                            {
                                foreach (GDLTerm term in relationTerms.Terms)
                                {
                                    foreach (int index in relationColumn.Value)
                                        UpdateArity(term, index, domain.Arity);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ExpandCompound(GDLTerm term, GDLArgument arg, int columnIndex)
        {
            term.Arguments.Remove(arg);
            int index = 0;
            foreach (GDLArgument nested in arg.Children)
            {
                if (nested.Compound)
                {
                    foreach (GDLArgument nested2 in nested.Children)
                    {
                        term.Arguments.Insert(columnIndex + index, nested2);
                        index++;
                    }
                }
                else
                {
                    term.Arguments.Insert(columnIndex + index, nested);
                    index++;
                }

            }
        }

        private void UpdateArity(GDLTerm term, int columnIndex, int newArity)
        {
            GDLArgument arg = term.Arguments[columnIndex];

            int oldArity = arg.Arity;
            int dif = newArity - oldArity;
            if (arg.Arity > 1 || arg.Constant)
            {
                if (arg.Compound)
                    ExpandCompound(term, arg, columnIndex);
                for (int i = 0; i < dif; ++i)
                {
                    GDLArgument emptyArg = new GDLArgument();
                    emptyArg.SetName(blankSymbolName);
                    emptyArg.Constant = true;
                    term.Arguments.Insert(columnIndex + oldArity, emptyArg);
                }
            }
            else
            {
                for (int i = 0; i < dif; ++i)
                {
                    GDLArgument dividedArg = new GDLArgument();
                    dividedArg.SetName(arg.Name + "_ms_" + i);
                    dividedArg.Constant = false;
                    term.Arguments.Insert(columnIndex + 1, dividedArg);
                }
            }
        }

        public void MapRuleVariable(GDLRule rule, GDLArgument arg, string conditionName, int columnIndex)
        {
            if (conditionName == "does")
                conditionName = "legal";

            Translator.Instance.InsertArgument(arg);

            if (arg.Constant)
                return;
            
            GDLArgumentDomain argDomain = ArgumentDomains.FirstOrDefault(a => a.Contains(conditionName, columnIndex));
            if (arg.Compound)
            {
                if (argDomain == null)
                {
                    argDomain = new GDLArgumentDomain(conditionName, columnIndex);
                    ArgumentDomains.Add(argDomain);
                }
                argDomain.Arity = Math.Max(arg.Arity, argDomain.Arity);
                return;
            }

            GDLArgumentDomain other = null;
            if (rule.DomainMapping.TryGetValue(arg.Name, out other))
            {
                if (argDomain == null)
                    other.Add(conditionName, columnIndex);
                else
                {
                    if (other != argDomain)
                    {
                        other.Join(argDomain);
                        ArgumentDomains.Remove(argDomain);
                        if (rule.DomainMapping.ContainsValue(argDomain))
                        {
                            List<string> keys = new List<string>();
                            foreach (var entry in rule.DomainMapping)
                            {
                                if (entry.Value == argDomain)
                                    keys.Add(entry.Key);

                            }
                            foreach (string key in keys)
                                rule.DomainMapping[key] = other;
                        }
                    }
                }
            }
            else
            {
                if (argDomain == null)
                {
                    argDomain = new GDLArgumentDomain(conditionName, columnIndex);
                    ArgumentDomains.Add(argDomain);
                }
                rule.DomainMapping.Add(arg.Name, argDomain);
            }

        }

        private void CalculateArity(GDLTerm fact)
        {
            AddToTermCollection(fact, AllTerms);
            for (int i = 0; i < fact.Arguments.Count; ++i)
            {
                Translator.Instance.InsertArgument(fact.Arguments[i]);
                if (fact.Arguments[i].Compound)
                {
                    GDLArgumentDomain argDomain = ArgumentDomains.FirstOrDefault(a => a.Contains(fact.Name, i));
                    if (argDomain == null)
                    {
                        argDomain = new GDLArgumentDomain(fact.Name, i);
                        ArgumentDomains.Add(argDomain);
                    }
                    argDomain.Arity = Math.Max(fact.Arguments[i].Arity, argDomain.Arity);
                }
            }
        }
        #endregion

        #region ADDING-DATA (PRE-PREPROCESSING)
        public void AddToRuleCollection(GDLRule rule, Dictionary<string, GDLRuleCollection> collections)
        {
            GDLRuleCollection col = null;
            if (collections.TryGetValue(rule.Header.Name, out col))
            {
                col.AddRule(rule);
            }
            else
            {
                col = new GDLRuleCollection();
                collections.Add(rule.Header.Name, col);
                col.AddRule(rule);
            }
        }
        public void AddToTermCollection(GDLTerm term, Dictionary<string, GDLTermCollection> collections)
        {
            GDLTermCollection col = null;
            if (collections.TryGetValue(term.Name, out col))
            {
                col.AddTerm(term);
            }
            else
            {
                col = new GDLTermCollection();

                collections.Add(term.Name, col);
                col.AddTerm(term);
            }
        }

        public void AddRuleWithOR_Distinct(GDLRule rule)
        {
            int index = 0;
            List<GDLTerm> distinctConditions = new List<GDLTerm>();
            foreach (GDLTerm condition in rule.Conditions)
            {
                if (condition.OR)
                {
                    foreach (GDLTerm child in condition.OrTerms)
                    {
                        if (child.Name.Equals("distinct"))
                        {
                            distinctConditions.Add(child);
                        }
                    }
                    break;
                }
                ++index;
            }
            if (distinctConditions.Count == 0)
            {
                rule.ContainedORDistinctInitally = false;
                Add(rule);
            }
            else
            {
                GDLTerm orCondition = rule.Conditions[index];
                orCondition.OrTerms.RemoveAll(a => a.Name.Equals("distinct"));
                if (orCondition.OrTerms.Count > 0)
                {
                    if (orCondition.OrTerms.Count == 1)
                        orCondition = orCondition.OrTerms[0];

                    Add(rule);
                }

                foreach (GDLTerm dCondition in distinctConditions)
                {
                    GDLRule clonedRule = rule.Clone();
                    clonedRule.Conditions.RemoveAt(index);
                    clonedRule.Conditions.Insert(index, dCondition);
                    Add(clonedRule);
                }
            }
        }

        public void AddConditionToAll(GDLTerm condition)
        {
            if (condition.DistinctKeyword)
                return;

            if (condition.OR)
            {
                foreach (GDLTerm t in condition.OrTerms)
                    AddConditionToAll(t);
                return;
            }

            AllConditions.Add(condition);
        }

        public void Add(GDLRule rule)
        {
           
            if (rule.OrCount > 1)
            {
                AddMultipleOR_Rule(rule);
                return;
            }
            if (rule.ContainedORDistinctInitally)
            {
                AddRuleWithOR_Distinct(rule);
                return;
            }
            string name = rule.Header.Name;
            if (name.Equals("base") == false && name.Equals("input") == false)
            {
                foreach (GDLTerm normalCondition in rule.Conditions)
                    AddConditionToAll(normalCondition);
                AllRules.Add(rule);
                if (rule.Header.InitKeyword)
                {
                    AddToRuleCollection(rule, InitRules);
                }
                else if (rule.Header.NextKeyword)
                {
                    AddToRuleCollection(rule, NextRules);
                }
                else
                {
                    AddToRuleCollection(rule, NonFrameRules);
                }
            }
        }

        public void AddMultipleOR_Rule(GDLRule rule)
        {
            int index = 0;
            List<GDLTerm> splitConditions = new List<GDLTerm>();
            foreach (GDLTerm condition in rule.Conditions)
            {
                if (condition.OR)
                {
                    foreach (GDLTerm child in condition.OrTerms)
                        splitConditions.Add(child);

                    break;
                }
                ++index;
            }

            foreach (GDLTerm dCondition in splitConditions)
            {
                GDLRule clonedRule = rule.Clone();
                clonedRule.OrCount--;
                clonedRule.Conditions.RemoveAt(index);
                clonedRule.Conditions.Insert(index, dCondition);
                Add(clonedRule);
            }

        }

        public void Add(GDLTerm term)
        {
            if (term.InitKeyword)
            {
                AddToTermCollection(term, InitFacts);
            }
            else
            {
                if (term.Name.Equals("base") == false && term.Name.Equals("input") == false)
                {
                    AddToTermCollection(term, ConstantFacts);
                }
                if (term.Name.Equals("role"))
                {
                    if (term.Arguments.Count > 0)
                        Translator.Instance.InsertArgument(term.Arguments[0]);
                }

            }
        } 
        #endregion

        #region PREPROCESSING
        public void OnFinished()
        {
            HandleRoleNames();
            CalculateArity();
            Translator.Instance.InsertBlank();
            UpdateArity();
            CustomizeRulesForConditions();
         
            CalculateVariablesState();
            PlayersCount = 0;
            if (ConstantFacts.ContainsKey("role"))
                PlayersCount = ConstantFacts["role"].Terms.Count;

            Arity_Legal = GetNamedArity("legal");
            
        }
        #region REORDERING OF CONDITIONS


        int ComplexityThresholdInit = 30;
        int ComplexityThresholdConstant = 7;

        HashSet<string> NoPriorityConditions = new HashSet<string>();
        HashSet<string> PriorityConditionsType1 = new HashSet<string>();
        HashSet<string> PriorityConditionsType2 = new HashSet<string>();

        public int GetConditionComplexity(GDLTerm condition)
        {
            if (condition.OR)
                return int.MaxValue;

            if(PriorityConditionsType1.Contains(condition.Name))
                return 0;

            if (condition.Query == null)
                condition.Query = new GDLQuery(condition);

            if (condition.Query.ConstraintsCount > 0)
            {
                if (PriorityConditionsType2.Contains(condition.Name))
                    return 0;

                return 2;
            }
            else
            {
                if (PriorityConditionsType2.Contains(condition.Name))
                    return 1;
            }

            return 3;
        }

        private void ComputeConditionsComplexity()
        {
            PriorityConditionsType1.Add("does");
            foreach (var entry in ConstantFacts)
            {
                int count = entry.Value.Terms.Count;
                if (count < ComplexityThresholdConstant)
                {
                    PriorityConditionsType1.Add(entry.Key);
                }
                else
                {
                    PriorityConditionsType2.Add(entry.Key);
                }
            }
            foreach (var entry in InitFacts)
            {
                int count = entry.Value.Terms.Count;
                if (count < ComplexityThresholdInit)
                {
                    PriorityConditionsType1.Add(entry.Key);
                }
                else
                {
                    PriorityConditionsType2.Add(entry.Key);
                }
            }
            foreach (var entry in NextRules)
            {
                if (PriorityConditionsType1.Contains(entry.Key) == false && PriorityConditionsType2.Contains(entry.Key) == false)
                    PriorityConditionsType2.Add(entry.Key);
            }
            foreach (var entry in ConditionRules)
            {
                GetRulesComplexityLevel(entry.Value.Rules);
            }
            foreach (var entry in ConditionRules)
            {
                foreach (GDLRule rule in entry.Value.Rules)
                {
                    ReorderConditions(rule);
                }
            }
        }

        public int GetRulesComplexityLevel(List<GDLRule> rules)
        {
            if (rules == null || rules.Count == 0)
                return 0;

            string ruleName = rules.First().Header.Name;

            if (NoPriorityConditions.Contains(ruleName))
                return 2;

            int complexity = 0;
            if (rules != null)
            {
                foreach (GDLRule r in rules)
                {
                    complexity = Math.Max(0, GetRuleComplexityLevel(r));
                    if (complexity == 2)
                        break;
                }
            }
            if (complexity == 0)
                PriorityConditionsType1.Add(ruleName);
            else if (complexity == 1)
                PriorityConditionsType2.Add(ruleName);
            else
                NoPriorityConditions.Add(ruleName);
          
            return complexity;
        }

        private int GetRuleComplexityLevel(GDLRule r)
        {
            if(r.Conditions.Count == 0)
                return 0;

            if(r.Conditions.Count > 1)
                return 2;

            GDLTerm condition = r.Conditions[0];

            if (condition.OR == false)
            {
                if (PriorityConditionsType1.Contains(condition.Name))
                    return 0;

                if (PriorityConditionsType2.Contains(condition.Name))
                    return 1;

                return 2;
            }
            else
            {
                int complexity = 0;
                foreach (GDLTerm c in condition.OrTerms)
                {
                    if (PriorityConditionsType1.Contains(condition.Name) == false)
                    {
                        if (PriorityConditionsType2.Contains(c.Name))
                            complexity = Math.Max(complexity, 1);
                        else
                        {
                            complexity = 2;
                            break;
                        }
                    }
                }
                return complexity;
            }
        }
     
        private void ReorderConditions(GDLRule rule)
        {
            if (rule.Conditions.Count < 2)
                return;

            GDLTerm firstCondition = rule.Conditions.First();
            if (firstCondition.OR)
                return;

            int firstComplexity = GetConditionComplexity(firstCondition);
            if (firstComplexity == 0)
                return;

          HashSet<string> variables = new HashSet<string>();
            firstCondition.ExtractVariables(variables);
           GDLTerm candidate =  rule.Conditions.FirstOrDefault(a => GetConditionComplexity(a) < firstComplexity && a.Name != rule.Header.Name && a.VariablesContainedIn(variables) && a.NOT ==false);
           if (candidate != null)
           {
               rule.Conditions.Remove(candidate);
               rule.Conditions.Insert(0, candidate);
           }
        }
	    #endregion
        
        private void HandleRoleNames()
        {
            GDLTermCollection tempCol = null;
            if (ConstantFacts.TryGetValue("role", out tempCol))
            {
                foreach (GDLTerm term in tempCol.Terms)
                {
                    string name = term.Arguments[0].Name;
                    if (RoleNames.Contains(name) == false)
                        RoleNames.Add(name);

                    
                }
            }

            Translator.Instance.CheckRoleNames(RoleNames);
        }
      
        private void CalculateVariablesState()
        {
            foreach (var entry in NonFrameRules)
            {
                foreach (GDLRule rule in entry.Value.Rules)
                    rule.CalculateVariablesState(); 
            }
            foreach (var entry in NextRules)
            {
                foreach (GDLRule rule in entry.Value.Rules)
                    rule.CalculateVariablesState();
                
            }
            foreach (var entry in InitRules)
            {
                foreach (GDLRule rule in entry.Value.Rules)
                    rule.CalculateVariablesState();
                
            }
        } 
	#endregion

        public GDLTerm CreateNamedCondition(string relationName, string playerName, int arity)
        {
            GDLTerm legalCondition = new GDLTerm();
            legalCondition.Name = relationName;
            legalCondition.Arguments.Add(new GDLArgument(playerName));
            for (int i = 1; i < arity; ++i)
            {
                legalCondition.Arguments.Add(new GDLArgument("?arg" + i));
            }
            return legalCondition;
        }

        public void UpdateConditionHash(GDLTerm condition)
        {
            string conditionTxt = condition.ToString();
            if (ConditionTextToID.TryGetValue(conditionTxt, out condition.HashID) == false)
            {
                condition.HashID = ConditionTextToID.Count;
                ConditionTextToID.Add(conditionTxt, condition.HashID);
            }
        }

        public void CustomizeRulesForConditions()
        {
            GDLRuleCollection rCol = null;
            List<GDLTerm> currentConditions = new List<GDLTerm>();
            List<GDLTerm> newConditions = new List<GDLTerm>();
            
            int legalArity = GetNamedArity("legal");
            int goalArity = GetNamedArity("goal");

            foreach (string roleName in RoleNames)
            {
                LegalConditions.Add(CreateNamedCondition("legal", roleName, legalArity));
                GoalConditions.Add(CreateNamedCondition("goal", roleName, goalArity));
            }  
            
            currentConditions.AddRange(LegalConditions);
            currentConditions.AddRange(GoalConditions);
            currentConditions.AddRange(AllConditions);
            while (currentConditions.Count > 0)
            {
                foreach (GDLTerm condition in currentConditions)
                {
                    if (condition.HashID < 0)
                        UpdateConditionHash(condition);

                    if (ConditionRules.ContainsKey(condition.HashID) == false)
                    {
                        GDLRuleCollection rules = new GDLRuleCollection();
                        if (NonFrameRules.TryGetValue(condition.Name, out rCol))
                            rules.Rules.AddRange(rCol.Rules);

                        if (rules.Rules.Count > 0)
                            CustomizeRulesForCondition(condition, rules, newConditions);
                    }
                }
                currentConditions.Clear();
                currentConditions.AddRange(newConditions);
                newConditions.Clear();

            }
        }

        public void CustomizeRulesForCondition(GDLTerm condition, GDLRuleCollection rules, List<GDLTerm> newConditions)
        {
            GDLQuery query = new GDLQuery();
            query.Consume(condition);
            if (query.ConstraintsCount == 0)
            {
                GDLRuleCollection outputRuleCollection = new GDLRuleCollection();
                foreach (GDLRule r in rules.Rules)
                {
                    if (condition.Arguments.Count == r.Header.Arguments.Count)
                        outputRuleCollection.AddRule(r);
                }
                if (outputRuleCollection.Rules.Count > 0)
                    ConditionRules.Add(condition.HashID, outputRuleCollection);
            }
            else
            {
                GDLRuleCollection outputRuleCollection = new GDLRuleCollection();
                foreach (GDLRule r in rules.Rules)
                {
                    GDLRule outRule = CustomizeRule(condition, r, query, newConditions);
                    if (outRule != null)
                    {
                        outRule.CalculateVariablesState();
                        outputRuleCollection.AddRule(outRule);
                    }
                }
                if (outputRuleCollection.Rules.Count > 0)
                    ConditionRules.Add(condition.HashID, outputRuleCollection);
            }
        }

        public GDLRule CustomizeRule(GDLTerm condition, GDLRule originalRule, GDLQuery query, List<GDLTerm> newConditions)
        {
            if (condition.Arguments.Count != originalRule.Header.Arguments.Count)
                return null;
            GDLRule rule = originalRule.Clone();
            foreach (GDLQuery_EqualVarSym es in query.EqualSymbol)
            {
                GDLArgument arg = rule.Header.Arguments[es.VarIndex];
                if (arg.Constant)
                {
                    if (arg.Name != es.Symbol)
                        return null;
                }
                else
                {
                    rule.ReplaceArgument(arg.Name, es.Symbol,newConditions);
                }
            }
            

            foreach (GDLQuery_EqualVarVar ev in query.EqualVariable)
            {
                GDLArgument arg1 = rule.Header.Arguments[ev.Var1Index];
                GDLArgument arg2 = rule.Header.Arguments[ev.Var2Index];

                if (arg1.Constant && arg2.Constant)
                {
                    if (arg1.Name != arg2.Name)
                        return null;
                }
                else if (arg1.Constant && arg2.Constant == false)
                    rule.ReplaceArgument(arg2.Name, arg1.Name,newConditions);
                else if (arg1.Constant == false && arg2.Constant)
                    rule.ReplaceArgument(arg1.Name, arg2.Name,newConditions);
                else 
                {
                    if (arg1.Name != arg2.Name)
                    {
                        rule.ReplaceArgument(arg2.Name, arg1.Name,newConditions);
                    }
                }
            }
            foreach (GDLTerm dc in rule.Conditions)
            {
                if (dc.DistinctKeyword)
                {
                    if (dc.Arguments[0].Name == dc.Arguments[1].Name)
                        return null;
                }
            }
            return rule;
        }
    }
}

