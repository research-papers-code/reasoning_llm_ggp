using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Parser.Compilers
{
    public class GDLCppTranslator
    {
        private CppConnector cppCompiler = null;
        public GDLCppTranslator()
        {
            cppCompiler = new CppConnector();
        }

        public void InitializeDictionaries()
        {
            foreach (var entry in Translator.Dictionary)
            {
                CppConnector.InsertSymbol(entry.Value, entry.Key);
            }
            foreach (var entry in Translator.FactNames)
            {
                CppConnector.InsertFact(entry.Value, entry.Key);
            }
        }
        public int CreateMapping(List<System.Drawing.Point> list, out GDLMapping mapping)
        {
            
            int[] local = new int[list.Count];
            int[] foreign = new int[list.Count];

            for (int i = 0; i < list.Count; ++i)
            {
                local[i] = list[i].Y;
                foreign[i] = list[i].X;
            }
             mapping = new GDLMapping(local, foreign);
            //else
            //{
            //    for (int i = 0; i < list.Count; ++i)
            //    {
            //        local[i] = list[i].X;
            //        foreign[i] = list[i].Y;
            //    }
            //}
            return CppConnector.CreateMapping(list.Count, local, foreign);
        }

        public bool Prove(NodeOR or)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
           bool result = CppConnector.ProveOR(or.ID);
           watch.Stop();
           Console.Write(result + " result. ");
           Console.WriteLine("Query time: " + watch.ElapsedMilliseconds + "[ms] /" + watch.ElapsedTicks);
           if (result)
           {
               int size = CppConnector.ExtractPublicDataSize();
               short[] data = new short[size * or.Arity];
               CppConnector.ExtractPublicData(data);
               Console.WriteLine("----RESULT---- Count = " + size);
               int index = 0;
               for (int i = 0; i < size; ++i)
               {
                   Console.Write("(");
                   for (int j = 0; j < or.Arity; ++j)
                   {
                       Console.Write(Translator.Instance.ToSymbol(data[index++]) + " ");
                   }
                   Console.WriteLine(")");
               }
           }
           return result;
        }
        public void CreateOR(NodeOR or)
        {
            CppConnector.CreateRowSet(or.Data);
            int _groundID = -1;
            int _exists = 0;
            if (or.Ground != null)
            {
                _groundID = or.Ground.ID;
                
            }
            int _queryID = -1;
            if (or.GroundQuery != null)
                _queryID = CppConnector.CreateQuery(or.GroundQuery);

            int _filterID = -1;
            if (or.GroundFilter != null)
            {
                int _filterMappingID = CreateMapping(or.GroundFilter.FromToMapping, out or.GroundFilter.Mapping);
                or.GroundFilter.FromToMapping = null;
               _filterID = CppConnector.CreateFilter(_filterMappingID, or.GroundFilter.Source.ID, or.GroundFilter.Distance);
            }
          
            CppConnector.CreateOR(or.ID, or.NameValue, or.Data.ID, _groundID, or.Mode, or.RealizationsCount, _queryID, _filterID, ref _exists);
            if (or.Realizations != null)
            {
                int index = 0;
                foreach (NodeAND nodeAND in or.Realizations)
                {
                    CreateAND(nodeAND);

                    int refValue = 20;
                    CppConnector.AddRealizationToOR(or.ID, nodeAND.ID, index, ref refValue);
                    ++index;
                }
            }
        }
       
        public void CreateAND(NodeAND and)
        {
            int index = 0;
            int[] dataRowsets = new int[and.PathsCount];
            for(int i=0; i<and.PathsCount; ++i)
            {
                CppConnector.CreateRowSet(and.Data[i]);
                dataRowsets[i] = and.Data[i]
                    .ID;
            }
            int _queryID = -1;
            if(and.ReturnQuery != null)
                _queryID = CppConnector.CreateQuery(and.ReturnQuery);

            int[] ids = null;
            if(and.PathsCount > 1)
                ids = and.Data.Select(a => a.ID).ToArray();
            and.ID = CppConnector.CreateAND(and.Operations.Count, and.PathsCount, and.MainData.ID, ids,_queryID);
            foreach (MergeOperation operation in and.Operations)
            {
                CreateOR_Operation(operation);
                CppConnector.AddOperationToAND(and.ID, operation.ID, index, operation.PathIndex);
                ++index;
            }
           
        }

        private void CreateOR_Operation(MergeOperation operation)
        {
            int _newMappingID = -1;
            int _commonMappingID = -1;


            if(operation.NewMapping != null && operation.NewMapping.LocalColumns != null && operation.NewMapping.LocalColumns.Length > 0)
             _newMappingID = CppConnector.CreateMapping(operation.NewMapping.Length, operation.NewMapping.LocalColumns, operation.NewMapping.ForeignColumns);
            if (operation.CommonMapping != null && operation.CommonMapping.LocalColumns != null && operation.CommonMapping.LocalColumns.Length > 0)
             _commonMappingID = CppConnector.CreateMapping(operation.CommonMapping.Length, operation.CommonMapping.LocalColumns, operation.CommonMapping.ForeignColumns);

            int _distinctQueryID = -1;
            if (operation.DistinctQuery != null)
                _distinctQueryID = CppConnector.CreateQuery(operation.DistinctQuery);

            int _filtersID = -1;
            if(operation.Filtering != null && operation.Filtering.Count >0)
                _filtersID = CppConnector.CreateFilters(operation.Filtering.Count);

            int operationBackColumnsLength = 0;
            if (operation.ForkBackColumns != null)
                operationBackColumnsLength = operation.ForkBackColumns.Length;
            int index=0;

            if (_filtersID >= 0)
            {
                foreach (Filter f in operation.Filtering.Filters)
                {
                    int _mappingID = CreateMapping(f.FromToMapping, out f.Mapping);
                    f.FromToMapping = null;
                    int _filterID = CppConnector.CreateFilter(_mappingID, f.Source.ID, f.Distance);
                    CppConnector.AddFilterToFilters(_filtersID, _filterID, index);
                    ++index;
                }
            }

            
            int _fromNodeID = -1;
            if (operation.FromNode != null)
            {
                CreateOR(operation.FromNode);
                _fromNodeID = operation.FromNode.ID;
            }
            operation.ID = CppConnector.CreateMergeOperation(_fromNodeID, _newMappingID, _commonMappingID, operation.MergeType, operation.ForkType, operation.ForkIntoCount, operation.ForkBackColumns, operationBackColumnsLength, _distinctQueryID, _filtersID);
         
        }



        internal void RunSimulation(int repeats)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            for(int i=0; i<repeats;++i)
                CppConnector.Simulator_Run();
            watch.Stop();
            Console.WriteLine("C++ Query time: " + watch.ElapsedMilliseconds + "[ms] /" + watch.ElapsedMilliseconds / Math.Max(repeats, 1) + "[ms]");
        }
    }
}
