using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Parser
{
    public class MergeOperation
    {
        public GDLTerm Condition = null;
        public int ID = -1;
        public int PathIndex = -1;

        public int ForkIntoCount { get; set; }
        public int MergeType;
        public int ForkType;

        public GDLMapping CommonMapping;
        public GDLMapping NewMapping;

        public NodeOR FromNode = null;
        public RowSet IntoData = null;

        public GDLQuery DistinctQuery;
        public FilterCollection Filtering;

        public DuplicateRemover duplicateKiller = null;
        public int[] ForkBackColumns = null;

        public Recurrence Recurr = null;


        public override string ToString()
        {
            return Condition.ToString();
        }
        public string MergeTypeToString()
        {
            switch (MergeType)
            {
                case 0:
                    return "NEW";
                case 1:
                    return "TRUE";
                case 2:
                    return "COMBINE";
                case 3:
                    return "VERIFY";
                case 4:
                    return "VER_NOT";
                case 5:
                    return "NEW_F";
                case 6:
                    return "TRUE_F";
                case 7:
                    return "COMBINE_F";
                default:
                    return "";
            }
        }

        public void DebugSerialize(System.IO.StreamWriter writer, string prefix)
        {
            string forkInformation = "";
            string mergeInformation = "";
            string distinctInformation = "";
            string recurrInformation = "";
            if (Recurr != null)
            {
                recurrInformation = " RECURRENCE";
            }
            if (ForkType == 1)
                forkInformation = "fork in " + PathIndex;
            else if (ForkType == 2)
                forkInformation = "fork back on " + PathIndex;

            if (ForkIntoCount > 0)
                forkInformation += "OF " + ForkIntoCount;
            mergeInformation = MergeTypeToString();
            if (DistinctQuery != null)
                distinctInformation = "Distincts:" + DistinctQuery.ToString();

            string forkBackVariables = "";
            if (ForkBackColumns != null)
            {
                forkBackVariables = " FB-COLUMNS={" + string.Join(" ", ForkBackColumns) + "}";
            }
            writer.Write(prefix + Condition);
            if (ForkType == 2)
                writer.WriteLine(prefix + " fork back" + forkBackVariables + recurrInformation);
            else
                writer.WriteLine(string.Format("{0}{1} {2}: New:{3} Common:{4} {5} {6}{7} OperationID={8}{9}", prefix, mergeInformation, forkInformation, NewMapping + "", CommonMapping + "", distinctInformation, Filtering + "", forkBackVariables, ID, recurrInformation));
        }

        internal void StopFiltering()
        {
            if (Filtering == null || Filtering.Filters.Count == 0)
            {
                if (DistinctQuery == null)
                    MergeType -= 8;
                else
                    MergeType -= 3;
                return;
            }
            Filtering.Visits++;
            if (Filtering.Visits > 9)
            {
                int c = Filtering.Filters.Count;
                if (Recurr == null)
                    Filtering.Filters.RemoveAll(a => a.ComputePerformance() < 0.11);
                else
                    Filtering.Filters.RemoveAll(a => a.ComputePerformance() < 0.01);

                if (DistinctQuery == null)
                    MergeType -= 8;
                else
                    MergeType -= 3;

                if (Filtering.Filters.Count == 0)
                    Filtering = null;
                else
                {
                    Filtering.OrderByPerformance();
                }
            }
        }
    }
}
