using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Parser
{
    public class FilterCollection
    {
        public List<Filter> Filters = new List<Filter>();
        public int Visits = 0;

        public override int GetHashCode()
        {
            return ComputeHashCode();
        }

        public int ComputeHashCode()
        {
            if (Filters.Count == 1)
                return Filters[0].GetHashCode();
            else
                return 2 * Filters[Filters.Count - 1].GetHashCode() + Filters[0].GetHashCode();
        }

        public override bool Equals(object obj)
        {
            FilterCollection other = obj as FilterCollection;
            if (other.Count != Count)
                return false;

            for (int i = 0; i < Filters.Count; ++i)
            {
                if (Filters[i].Equals(other.Filters[i]) == false)
                    return false;
            }
            return true;
        }

        public override string ToString()
        {
            if (Filters.Count > 0)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("FILTER");
                foreach (Filter f in Filters)
                {
                    builder.Append("{");
                    builder.Append(f.ToString());
                    builder.Append("}");
                }
                return builder.ToString();
            }
            else
                return "";
        }

        public int Count
        {
            get
            {
                return Filters.Count;
            }
        }

        public void ApplyPassedForRule(GDLRule rule)
        {
            foreach (Filter f in Filters)
                f.ApplyPassedForRule(rule);
        }

        public void ApplyPassedForCondition(GDLRule rule, NodeAND nodeAND, bool conditionHasRules, VariablesOperation operation, MergeOperation mergeOperation, out FilterCollection toPass, out FilterCollection toApply, HashSet<string> roleNames)
        {
            toPass = new FilterCollection();
            toApply = new FilterCollection();
            foreach (Filter f in Filters)
            {
                Filter mappedFilter = f.PassPassedForCondition(operation.RelatedCondition);
                if (mappedFilter != null)
                {
                    if (conditionHasRules)
                    {
                        toPass.AddFilter(mappedFilter);
                    }
                }
                Filter appliedFilter = f.ApplyForCondition(operation, mergeOperation, nodeAND);
                if (appliedFilter != null)
                {
                    toApply.AddFilter(appliedFilter);
                }
            }
            // if (conditionHasRules == false)
            {
                Filter localPass = Filter.CreateToPassForCondition(rule, operation.RelatedCondition, operation, roleNames);
                if (localPass != null)
                {
                    toPass.AddFilter(localPass);
                }
            }
        }

        public static int TotalFilters = 0;
        public void AddFilter(Filter appliedFilter)
        {
            foreach (Filter f in Filters)
            {
                int result = Filter.Compare(f, appliedFilter);
                if (result > 0)
                    return;

                if (result < 0)
                {
                    Filters.Remove(f);
                    Filters.Add(appliedFilter);
                    return;
                }
            }

            Filters.Add(appliedFilter);
        }

        public FilterCollection Clone()
        {
            FilterCollection clone = new FilterCollection();
            foreach (Filter f in Filters)
                clone.Filters.Add(f.ClonePreprocessing());
            return clone;
        }

        public FilterCollection CloneRuntime()
        {
            FilterCollection clone = new FilterCollection();
            foreach (Filter f in Filters)
                clone.Filters.Add(f.CloneRuntime());
            return clone;
        }

        public List<FilterQuery> ReduceByQuery(GDLQuery query)
        {
            List<FilterQuery> list = null;
            foreach (Filter f in Filters)
            {
                FilterQuery newFilterQ = new FilterQuery(f, query);
                if (newFilterQ.Empty == false)
                {
                    if (list == null)
                        list = new List<FilterQuery>();

                    bool add = true;
                    foreach (FilterQuery filterQuery in list)
                    {
                        int result = Filter.Compare(filterQuery.Filter, newFilterQ.Filter);
                        if (result > 0)
                        {
                            add = false;
                            break;
                        }

                        if (result < 0)
                        {
                            list.Remove(filterQuery);
                            add = true;
                            break;
                        }
                    }
                    if (add)
                        list.Add(newFilterQ);
                }
            }
            return list;
        }

        public Filter ChooseOne()
        {
            if (Count == 1)
                return Filters[0];

            Filter best = Filters.First();
            foreach (Filter f in Filters)
            {
                if (f.Source.Count < best.Source.Count)
                    best = f;
            }
            return best;
        }

        public void OrderByPerformance()
        {
            if (Count < 2)
                return;

            float best = -1;
            float current;
            int bestIndex = 0;
            for (int i = 0; i < Count; ++i)
            {
                current = Filters[i].ComputePerformance();
                if (current > best)
                {
                    best = current;
                    bestIndex = i;
                }
            }
            if (bestIndex > 0)
            {
                Filter temp = Filters[bestIndex];
                Filters[bestIndex] = Filters[0];
                Filters[0] = temp;
            }
        }
    }
}
