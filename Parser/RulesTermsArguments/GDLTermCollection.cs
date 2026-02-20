using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class GDLTermCollection
    {
        public List<GDLTerm> Terms = new List<GDLTerm>();
        internal void AddTerm(GDLTerm term)
        {
            Terms.Add(term);
        }

        public override string ToString()
        {
            return Terms.Count + " terms";
        }
    }
}
