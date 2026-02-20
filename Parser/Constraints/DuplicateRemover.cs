using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class DuplicateRemover
    {
        public int VisitCount;
        public int TotalDeleted;
        public bool Disabled;
        
        public int LIMIT = 30;

        public int Arity;
        public DuplicateRemover(int arity)
        {
            Arity = arity;
            VisitCount = 0;
            TotalDeleted = 0;
            Disabled = false;
        }

        public void Run(RowSet rowset, int start)
        {
            if (Disabled)
                return;

            if (VisitCount > LIMIT)
                rowset.DeleteDuplicates(start, Arity);
            else
            {
                TotalDeleted += rowset.DeleteDuplicates(start, Arity);
                ++VisitCount;
                if (VisitCount > LIMIT && TotalDeleted == 0)
                {
                    Disabled = true;
                }
            }
        }
        public void Run(RowSet rowset, int[] backColumns)
        {
            if (Disabled)
                return;

            if (VisitCount > LIMIT)
                rowset.DeleteDuplicates(backColumns);
            else
            {
                TotalDeleted += rowset.DeleteDuplicates(backColumns);
                ++VisitCount;
                if (VisitCount > LIMIT && TotalDeleted == 0)
                    Disabled = true;
            }
        }

        public void Run(RowSet rowset, int[] backColumns, int start)
        {
            if (Disabled)
                return;

            if (VisitCount > LIMIT)
                rowset.DeleteDuplicates(backColumns, start);
            else
            {
                TotalDeleted += rowset.DeleteDuplicates(backColumns,start);
                ++VisitCount;
                if (VisitCount > LIMIT && TotalDeleted == 0)
                    Disabled = true;
            }
        }
    }
}
