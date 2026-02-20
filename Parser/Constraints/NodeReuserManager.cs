using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser.Constraints
{
    public class NodeReuserManager
    {
        Dictionary<string, List<NodeOR>> SourceMap = new Dictionary<string, List<NodeOR>>();
        public Dictionary<string, List<NodeOR>> ConsumerMap = new Dictionary<string, List<NodeOR>>();
        public bool IsRegisteringFinished = false;
        public List<ReusableNodeOR> DataFeed = new List<ReusableNodeOR>();

        public void OnNewState()
        {
            foreach (ReusableNodeOR node in DataFeed)
            {
                node.Data = null;
                node.Result = false;
            }
        }
        public void RegisterSource(string name, NodeOR node)
        {
            List<NodeOR> temp = null;
            if (SourceMap.TryGetValue(name, out temp))
            {
                temp.Add(node);
            }
            else
                SourceMap.Add(name, new List<NodeOR>() { node });
        }

        public void RegisterConsumer(string name, NodeOR node)
        {
            List<NodeOR> temp = null;
            if (ConsumerMap.TryGetValue(name, out temp))
            {
                temp.Add(node);
            }
            else
                ConsumerMap.Add(name, new List<NodeOR>() { node });
        }

        public void RegisteringFinished()
        {
           IsRegisteringFinished = true;
           var pairList = ConsumerMap.Where(a => a.Value.Count <= 1 ||  SourceMap.ContainsKey(a.Key)==false).ToList();
          
           foreach (var entry in pairList)
           {
               ConsumerMap.Remove(entry.Key);
               SourceMap.Remove(entry.Key);
               foreach (NodeOR node in entry.Value)
               {
                   node.ReuseLink = null;
               }
           }
            /*source is also technically a consumer*/
           int id = 0;           
           foreach (var entry in ConsumerMap)
           {
               ReusableNodeOR reusableNode = new ReusableNodeOR(id++);
               DataFeed.Add(reusableNode);
               foreach (NodeOR node in entry.Value)
               {
                   node.ReuseLink.Node = reusableNode;
               }
           }
        }
    }
}
