using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Parser.Constraints;
using Parser.Simulator;

namespace Parser
{
    public class NodeOR
    {
        public int ID = -1;
        public RowSet Data = null;
        public NodeOR Ground = null;
        
        public FilterCollection GroundFilter = null;
       // public ReusableNodeOR ReusableNode = null;

        public int Arity { get; set; }
        public int Mode { get; set; }
        public List<NodeAND> Realizations = null;

        public GDLQuery GroundQuery { get; set; }

        public string Name { get; set; }
        public int NameValue { get; set; }

        public static int ID_SEED = 0;

        public NodeAND Parent = null;

        public ReuseLink ReuseLink;

        /*---------------------------------------------------------------------------------------------------------------------------------------*/
        public NodeOR(string relationName, NodeOR groundOR, int arity, NodeAND parent)
        {
            ID = ID_SEED++;
            Arity = arity;
            Name = relationName;
            NameValue = Translator.Instance.FactToValue(relationName);
            Ground = groundOR;
            Parent = parent;
        }

        public int RealizationsCount
        {
            get
            {
                if (Realizations == null)
                    return 0;

                return Realizations.Count;
            }
        }

        public NodeAND GoUpAND(int distance)
        {
            NodeAND current = Parent;
            for (int i = 1; i < distance; ++i)
			{ 
                current = current.Parent.Parent;
            }

            return current;
        }
       
        public bool ProveReuse()
        {
            if (ReuseLink.Node.Result == false)
                return false;
          
            if (GroundFilter != null)
            {
                Data.RewriteFilterQuery(ReuseLink.Node.Data, GroundQuery, GroundFilter.ChooseOne());
            }
            else if (GroundQuery != null)
            {
                Data.RewriteWithQuery(ReuseLink.Node.Data, GroundQuery);
            }
            else
            {
                Data = ReuseLink.Node.Data;
                return true; 
            }
            return Data.Count > 0;
        }

        public void UpdateMode()
        {
            int filtersCount = GroundFilter.Count;
            GroundFilter.Filters.RemoveAll(a => a.Established == false);
            if (filtersCount == GroundFilter.Filters.Count && GroundFilter.Count > 0)
            {
                if (GroundQuery == null)
                {
                    Mode = -2;
                }
                else
                {
                    Mode = -3;
                }
            }
            else if (GroundFilter.Filters.Count == 0)
            {
                if (GroundQuery != null)
                {
                    Mode = 1;
                }
                else
                {
                    Data = Ground.Data;
                    Mode = 0;
                }
            }
            else
            {
                GroundFilter.Visits -= 10;
            }
        }

        public bool Prove()
        {
            //if (ReuseLink != null) //TODO
            //{
            //    if (ReuseLink.Node.Data != null)
            //    {
            //        return ProveReuse();
            //    }
            //    else if (ReuseLink.ProducerConsumer)
            //        return ProveProduce();
            //}
            switch (Mode)
            {
                case -3:
                    Data.RewriteFilterQuery_Hasher(Ground.Data, GroundQuery, GroundFilter.ChooseOne());
                    return Data.Count > 0;
                case -2:
                    Data.RewriteFilter_Hasher(Ground.Data, GroundFilter.ChooseOne());
                    return Data.Count > 0;
                case -1:
                    Data.RewriteLearning_Hasher(Ground.Data, GroundQuery, GroundFilter.ChooseOne());
                    ++GroundFilter.Visits;

                    if (GroundFilter.Visits > 10)
                        UpdateMode();
                    return Data.Count > 0;
                case 0:
                    return Data.Count > 0;
                case 1:
                    if (Ground.Data.Count > 4)
                        Data.RewriteWithQuery_Hasher(Ground.Data, GroundQuery);
                    else
                        Data.RewriteWithQuery(Ground.Data, GroundQuery);
                    return Data.Count > 0;
                case 2:
                    Data.Rewrite(Ground.Data);
                    break;
                case 3:
                    if (Ground.Data.Count > 4)
                        Data.RewriteWithQuery_Hasher(Ground.Data, GroundQuery);
                    else
                        Data.RewriteWithQuery(Ground.Data, GroundQuery);
                    break;
                default:
                    break;
            }

            bool success = false;
            if (Realizations[0].Prove())
            {
                success = true;
                if (Mode < 4)
                    Data.MergeAdd(Realizations[0].MainData);
            }
            else
            {
                if (Mode == 4)
                    Data.Clear();
            }

            for (int i = 1; i < RealizationsCount; ++i)
            {
                if (Realizations[i].Prove())
                {
                    int oldcount = Data.Count;
                    Data.MergeAdd(Realizations[i].MainData);
                    Realizations[i].duplicateKiller.Run(Data, oldcount);
                    success = true;
                }

            }
            return Data.Count > 0 || success;
        }

        private bool ProveProduce()
        {
            switch (Mode)
            {
                case 2:
                    Data.Rewrite(Ground.Data);
                    ReuseLink.Node.Result = Data.Count > 0;
                    break;
                case 3:
                    if (Ground.Data.Count > 4)
                        Data.RewriteWithQuery_Hasher(Ground.Data, GroundQuery);
                    else
                        Data.RewriteWithQuery(Ground.Data, GroundQuery);
                    ReuseLink.Node.Result = Data.Count > 0;
                    break;
                default:
                    break;
            }

            if (Realizations[0].Prove())
            {
                 ReuseLink.Node.Result = true;
                if (Mode < 4)
                    Data.MergeAdd(Realizations[0].MainData);
            }
            else
            {
                if (Mode == 4)
                    Data.Clear();
            }

            for (int i = 1; i < RealizationsCount; ++i)
            {
                if (Realizations[i].Prove())
                {
                    int oldcount = Data.Count;
                    Data.MergeAdd(Realizations[i].MainData);
                    Realizations[i].duplicateKiller.Run(Data, oldcount);
                    ReuseLink.Node.Result = true;
                }

            }
            ReuseLink.Node.Data = Data;
            return ReuseLink.Node.Result;
        }

        /*----------------------------------------*/
        public void CorrectFilters()
        {
            if (GroundFilter != null)
            {
                foreach (Filter f in GroundFilter.Filters)
                    f.Source = GoUpAND(f.Distance).Data[f.SourcePathIndex];
            }
            if (Realizations != null)
            {
                foreach (NodeAND realization in Realizations)
                    realization.CorrectFilters();
            }
        }
    }
}