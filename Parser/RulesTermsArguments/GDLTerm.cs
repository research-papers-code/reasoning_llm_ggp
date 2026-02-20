using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class GDLTerm
    {
        public static int TermsCount = 0;
        public string Name;
        public List<GDLArgument> Arguments = new List<GDLArgument>();

        public bool NOT = false;
        public bool OR = false;
        public bool TRUE = false;
        public bool NOTOR = false;

        public bool NextKeyword = false;
        public bool InitKeyword = false;
        public List<GDLTerm> OrTerms = null;
        public bool DistinctKeyword;

        public int HashID = -1;

        public int c;
        public int ConditionID
        {
            set { c = value; }
            get { return HashID; }
        }

        public GDLQuery Query = null;
        public static int ID_SEED = 0;

        public GDLTerm()
        {
            TermsCount++;
            ConditionID = ID_SEED++;

        }

        public GDLTerm FirstNegatedOrNULL()
        {
            if (NOT)
                return this;

            if (OrTerms != null)
            {
                foreach (GDLTerm child in OrTerms)
                {
                    if (child.NOT)
                        return child;
                }
            }
            return null;
        }

        public void ExtractVariables(HashSet<string> variableSet)
        {
            if (OrTerms == null)
            {
                foreach (GDLArgument arg in Arguments)
                    arg.ExtractVariables(variableSet);
            }
            else
            {
                foreach(GDLTerm child in OrTerms)
                child.ExtractVariables(variableSet);
                
            }
        }

        public override string ToString()
        {
            string txt;
            if (OR)
            {
                txt = "OR{";
                foreach (GDLTerm term in OrTerms)
                {
                    txt += term.ToString();
                }
                return txt + "}";
            }

            if (NOT) 
                txt = "(NOT " + Name+" ";
            else
                txt = "(" + Name+" ";
 
            foreach (GDLArgument var in Arguments)
            {
                txt += var.ToString();
            }
            return txt + ")";
        }

        public GDLTerm Clone()
        {
            GDLTerm clone = new GDLTerm();
            foreach (GDLArgument arg in Arguments)
                clone.Arguments.Add(arg.Clone());

            clone.Name = Name;
            clone.NOT = NOT;
            clone.OR = OR;
            clone.NOTOR = NOTOR;
            clone.TRUE = TRUE;
            clone.DistinctKeyword = DistinctKeyword;
            clone.NextKeyword = NextKeyword;
            clone.InitKeyword = InitKeyword;
            if (OR)
            {
                clone.OrTerms = new List<GDLTerm>();
                foreach (GDLTerm orTerm in OrTerms)
                    clone.OrTerms.Add(orTerm.Clone());
            }
            return clone;
        }

        public void ReplaceArgument(string oldName, string newName, List<GDLTerm> newConditions)
        {
            if (OR == false)
            {
                foreach (GDLArgument arg in Arguments)
                {
                    if (arg.Name == oldName)
                    {
                        arg.SetName(newName);
                        arg.Constant = newName[0] != '?';
                        if (newConditions.Contains(this) == false)
                            newConditions.Add(this);
                    }
                }
            }
            else
            {
                foreach (GDLTerm orTerm in OrTerms)
                    orTerm.ReplaceArgument(oldName, newName,newConditions);
            }
        }

        public int FirstArgumentIndexOf(string varName)
        {
            for (int i = 0; i < Arguments.Count; ++i)
            {
                if(Arguments[i].Name.Equals(varName))
                    return i;
            }
            return -1;
        }

        public int CurrentAriy
        {
            get { return Arguments.Count; }
        }

        public bool VariablesContainedIn(HashSet<string> variables)
        {
            int variablesCount = 0;
            for (int i = 0; i < Arguments.Count; ++i)
            {
                GDLArgument a = Arguments[i];
                if (a.Constant == false)
                {
                    ++variablesCount;
                    if(variables.Contains(a.Name))
                        return true;
                }

            }
            return  variablesCount == 0;
        }

        public int CountDuplicatesEx(out int variablesCount)
        {
            int dupliCount =0;
            variablesCount = 0;
            for (int i = 0; i < Arguments.Count; ++i)
            {
                GDLArgument a = Arguments[i];
                if (a.Constant == false)
                    ++variablesCount;
                for (int j = 0; j < i; ++j)
                {
                    if (a.Name == Arguments[j].Name)
                    {
                        ++dupliCount;
                        break;
                    }
                }
            }
            return dupliCount;
        }
    }
}
