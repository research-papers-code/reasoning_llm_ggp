using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser.Constraints
{
    public class ReusableNodeOR
    {
        public RowSet Data;
        public int ID;
        
        public bool Result;

        public ReusableNodeOR(int id)
        {
            ID = id;
            Result = false;
            Data = null;
        }
    }
}
