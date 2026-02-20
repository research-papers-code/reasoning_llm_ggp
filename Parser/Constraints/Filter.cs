using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Parser
{
    public class Filter
    {
        public int Visits = 0;
        public int FilteredCount = 0;
        public RowSet Source = null;
        public int Distance = 0;
        public int SourcePathIndex = 0;

        public List<Point> FromToMapping = new List<Point>();
        public bool Established = false;
        public GDLMapping Mapping = null;
        public List<string> VariableNames = new List<string>();

        int luckyIndex = 0;

        public float ComputePerformance()
        {
            if (Visits == 0)
                return 0;

            if (Visits > 0 && Visits < 4)
                return 1.0f;

            return (float)FilteredCount / (float)Visits;
        }
        public bool PassRow_L(RowSet data, int rowPtr)
        {
            ++Visits;
            int index = Source.FindNextRow(0, Mapping, data.Data, rowPtr);
            if (index < 0)
            {
                ++FilteredCount;
                Established = true;
                return false;
            }
            luckyIndex = index;
            return true;
        }
		public bool PassRow(RowSet data, int rowPtr)
		{
			if (Source.FindNextRow(luckyIndex, Mapping, data.Data, rowPtr) >= 0)
				return true;

			if (luckyIndex >= Source.Count)
				luckyIndex = Source.Count;
			return Source.FindNextRow(0, luckyIndex - 1, Mapping, data.Data, rowPtr) >= 0;
		}

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("D:" + Distance + "ID:" + Source.ID);
            builder.Append(Mapping.ToString());
            return builder.ToString();
        }

        public void CorrectMapping()
        {
            int[] local = new int[FromToMapping.Count];
            int[] foreign = new int[FromToMapping.Count];

            for (int i = 0; i < FromToMapping.Count; ++i)
            {
                local[i] = FromToMapping[i].Y;
                foreign[i] = FromToMapping[i].X;
            }
            Mapping = new GDLMapping(local, foreign);
        }

        /// <summary>
        /// Returns mapped of a filteredVarIndex onto filteringVariableIndex. -1 if a filtered (local) variable is not used.
        /// </summary>
        /// <param name="filteredVariableIndex"></param>
        /// <returns></returns>
        public int GetSourceVarIndex(int localVariableIndex)
        {
            foreach (Point p in FromToMapping)
            {
                if (p.Y == localVariableIndex)
                    return p.X;
            }
            return -1;
        }

        public Filter ClonePreprocessing()
        {
            Filter clone = new Filter(Source, Distance, SourcePathIndex);
            clone.VariableNames.AddRange(VariableNames);

            for (int i = 0; i < Mapping.Length; ++i)
                clone.FromToMapping.Add(new Point(Mapping.ForeignColumns[i], Mapping.LocalColumns[i]));
            clone.CorrectMapping();      
            return clone;
        }

        public Filter CloneRuntime()
        {
            Filter clone = new Filter(Source, Distance, SourcePathIndex);
            clone.Mapping = new GDLMapping(Mapping.Length);
            for (int i = 0; i < Mapping.Length; ++i)
            {
                clone.Mapping.LocalColumns[i] = Mapping.LocalColumns[i];
                clone.Mapping.ForeignColumns[i] = Mapping.ForeignColumns[i];
            }
            clone.Visits = Visits;
            return clone;
        }

        private Filter(RowSet from, int distance, int sourcePath)
        {
            Source = from;
            Distance = distance;
            SourcePathIndex = sourcePath;
        }

        public bool Empty
        {
            get { return FromToMapping.Count == 0; }
        }

        public void ApplyPassedForRule(GDLRule rule)
        {
            VariableNames.Clear();
            foreach (Point p in FromToMapping)
                VariableNames.Add(rule.Header.Arguments[p.Y].Name);
        }

        public Filter PassPassedForCondition(GDLTerm condition)
        {
            Filter filter = new Filter(Source, Distance + 1, SourcePathIndex);
            for (int i = 0; i < FromToMapping.Count; ++i)
            {
                Point map = new Point();
                map.X = FromToMapping[i].X;

                string varName = VariableNames[i];
                map.Y = condition.FirstArgumentIndexOf(varName);
                if (map.Y >= 0)
                {
                    filter.VariableNames.Add(varName);
                    filter.FromToMapping.Add(map);
                }
            }
            if (filter.FromToMapping.Count > 0)
            {
                filter.CorrectMapping();
                return filter;
            }
            else
                return null;
        }

        public static Filter CreateToPassForCondition(GDLRule rule, GDLTerm condition, VariablesOperation operation, HashSet<string> roleNames)
        {
            if (operation.CommonVariables.Count == 0)
                return null;

            Filter filter = new Filter(null, 1,0);
            foreach (string varName in operation.CommonVariables)
            {
                if (roleNames.Contains(varName) == false)
                {
                    Point map = new Point();
                    map.X = rule.VariablesMapping[varName];
                    map.Y = condition.FirstArgumentIndexOf(varName);
                    filter.FromToMapping.Add(map);
                }
            }
            if (filter.FromToMapping.Count == 0)
                return null;
            filter.CorrectMapping();

            
            return filter;
        }

        public bool CanBeAppliedForCondition(VariablesOperation operation)
        {
            foreach (string varName in VariableNames)
            {
                if (operation.NewVariables.Contains(varName))
                    return true;
            }
            return false;
        }

        public Filter ApplyForCondition(VariablesOperation operation, MergeOperation mergeOperation, NodeAND and)
        {
            if (CanBeAppliedForCondition(operation) == false)
                return null;

            Filter clone = new Filter(Source, Distance,SourcePathIndex);

            foreach (Point p in FromToMapping)
            {
                if (and.Operations.FirstOrDefault(a => (a.NewMapping != null && a.NewMapping.LocalColumns.Contains(p.Y))) != null || mergeOperation.NewMapping.LocalColumns.Contains(p.Y))
                    clone.FromToMapping.Add(p);
            }
           
            if (clone.Empty)
                return null;

            clone.CorrectMapping();

           
            return clone;
        }

        public static int Compare(Filter f1, Filter f2)
        {
            int commonCount = 0;
            int l1 = f1.FromToMapping.Count;
            int l2 = f2.FromToMapping.Count;
            foreach (Point p in f1.FromToMapping)
            {
                if(f2.FromToMapping.Exists(a => a.Y == p.Y))
                    ++commonCount;
            }

            if (commonCount == l1 && commonCount < l2)
                return -1;

            if (commonCount == l2 && commonCount < l1 && f1.Distance <= f2.Distance)
                return 1;

            if (commonCount == l1 && commonCount == l2)
            {
                if (f2.Distance > f1.Distance)
                    return 1;
                else
                    return -1;
            }
            return 0;
        }

        public override int GetHashCode()
        {
            int code = Distance;
            code += (Distance + Mapping.ForeignColumns[Mapping.Length - 1]);
            code += (Distance + Mapping.LocalColumns[Mapping.Length - 1]);
            return code;
        }

        public override bool Equals(object obj)
        {
            Filter other = obj as Filter;

            if (Distance != other.Distance)
                return false;

            if (SourcePathIndex != other.SourcePathIndex)
                return false;

            if (Mapping.Length != other.Mapping.Length)
                return false;

            for (int i = 0; i < Mapping.Length; ++i)
            {
                if (Mapping.ForeignColumns[i] != other.Mapping.ForeignColumns[i])
                    return false;

                if (Mapping.LocalColumns[i] != other.Mapping.LocalColumns[i])
                    return false;
            }
            return true;
        }
    }
}
