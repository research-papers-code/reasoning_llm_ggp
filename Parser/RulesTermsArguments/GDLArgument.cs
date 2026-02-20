using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    public class GDLArgument
    {
        public string Name
        {
            private set;
            get;
        }

        public void SetName(string name)
        {
            Name = name;
            if (name[0] != '?')
                Translator.Instance.InsertSymbol(name);
        }

        public bool Constant;
   
        public GDLArgument()
        {

        }

        public GDLArgument(string name)
        {
            SetName(name);
            Constant = name[0] != '?';
        }

        public List<GDLArgument> Children = null;
        public int GlobalArity = 1;

        public GDLArgument Clone()
        {
            GDLArgument clone = new GDLArgument();
            clone.Name = Name;
            clone.Constant = Constant;
            clone.GlobalArity = GlobalArity;

            if (Children != null)
            {
                clone.Children = new List<GDLArgument>();
                foreach (GDLArgument child in Children)
                    clone.Children.Add(child.Clone());
            }
            return clone;
        }
        public bool Compound
        {
            get { return Children != null; }
        }

        public void UpdateGlobalArity(int arity)
        {
            if (arity > GlobalArity)
                GlobalArity = arity;
        }
        public int Arity
        {
            get
            {
                if (Compound == false)
                    return 1;
                else
                {
                    int arity = 0;
                    foreach (GDLArgument arg in Children)
                        arity += arg.Arity;
                    return arity;
                }

            }
        }

        public override string ToString()
        {
            if (Compound == false)
            {
                return Name + " ";
            }
            else
            {
                string txt = "( ";
                foreach (GDLArgument var in Children)
                    txt += var.ToString();
                return txt + ")";
            }
        }

        public void ExtractVariables(HashSet<string> variableSet)
        {
            if (Compound)
            {
                foreach (GDLArgument child in Children)
                    child.ExtractVariables(variableSet);
            }
            else
            {
                if (Constant == false)
                    variableSet.Add(Name);
            }
        }

        public void ExtractNewVariables(HashSet<string> unsafeVariables, HashSet<string> oldVariables)
        {
            if (Compound)
            {
                foreach (GDLArgument child in Children)
                    child.ExtractNewVariables(unsafeVariables, oldVariables);
            }
            else
            {
                if (Constant == false && oldVariables.Contains(Name)==false)
                    unsafeVariables.Add(Name);
            }
        }
    }
}
