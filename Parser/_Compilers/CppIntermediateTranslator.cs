using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Parser.Simulator;
using Parser.Serialization;

namespace Parser.Compilers
{
    public class CppIntermediateTranslator
    {
        private CppConnector cppCompiler = null;
        public CppIntermediateTranslator()
        {
            cppCompiler = new CppConnector();
        }

        public void InitializeDictionaries(int interpreterID)
        {
            foreach (var entry in Translator.Dictionary)
            {
                CppConnector.InsertSymbol(entry.Value, entry.Key, interpreterID);
            }
            foreach (var entry in Translator.FactNames)
            {
                CppConnector.InsertFact(entry.Value, entry.Key, interpreterID);
            }
        }
        public int CreateMapping(List<System.Drawing.Point> list, out GDLMapping mapping, int interpreterID)
        {
            int[] local = new int[list.Count];
            int[] foreign = new int[list.Count];

            for (int i = 0; i < list.Count; ++i)
            {
                local[i] = list[i].Y;
                foreign[i] = list[i].X;
            }
            mapping = new GDLMapping(local, foreign);
            return CppConnector.CreateMapping(list.Count, local, foreign, interpreterID);
        }

        
        public void CreateOR(NodeOR or, int interpreterID)
        {
            CppConnector.CreateRowSet(or.Data, interpreterID);
            int _groundID = -1;
            if (or.Ground != null)
            {
                _groundID = or.Ground.ID;
                
            }
            int _queryID = -1;
            if (or.GroundQuery != null)
                _queryID = CppConnector.CreateQuery(or.GroundQuery, interpreterID);

            int _filtersID = CreateFilters(or.GroundFilter, interpreterID);

            int _parentID = -1;
            if(or.Parent != null)
                _parentID = or.Parent.ID;


            CppConnector.CreateOR(or.ID, or.NameValue, _parentID, or.Data.ID, _groundID, or.Mode, or.RealizationsCount, _queryID, _filtersID, interpreterID); 
            if (or.Realizations != null)
            {
                int index = 0;
                foreach (NodeAND nodeAND in or.Realizations)
                {
                    CreateAND(nodeAND, interpreterID);
                    CppConnector.AddRealizationToOR(or.ID, nodeAND.ID, index, interpreterID);
                    ++index;
                }
            }
        }
       
        public void CreateAND(NodeAND and, int interpreterID)
        {
        
            int[] dataRowsets = new int[and.PathsCount];
            for (int i = 0; i < and.PathsCount; ++i)
            {
                CppConnector.CreateRowSet(and.Data[i], interpreterID);
                dataRowsets[i] = and.Data[i].ID;
            }

            int _queryID = -1;
            if (and.ReturnQuery != null)
                _queryID = CppConnector.CreateQuery(and.ReturnQuery, interpreterID);

            int[] ids = null;
            if (and.PathsCount > 1)
                ids = and.Data.Select(a => a.ID).ToArray();

            int _parentID = -1;
            if (and.Parent != null)
                _parentID = and.Parent.ID;

            int _fqCount = -1;
            if (and.ReturnFilterQuery != null)
                _fqCount = and.ReturnFilterQuery.Count;
            and.ID = CppConnector.CreateAND(_parentID, and.Operations.Count, and.PathsCount, _fqCount, and.MainData.ID, ids, _queryID, and.duplicateKiller.Arity, and.duplicateKiller.LIMIT, interpreterID);

           
            int index = 0;
            if (and.ReturnFilterQuery != null)
            {
                foreach (FilterQuery fq in and.ReturnFilterQuery)
                    CppConnector.AddFilterQueryToAND(and.ID, CreateFilter(fq.Filter, interpreterID), CppConnector.CreateQuery(fq.Query, interpreterID), index++, interpreterID);

            }
            index = 0;
            foreach (MergeOperation operation in and.Operations)
            {
                CreateOR_Operation(operation, interpreterID);
                CppConnector.AddOperationToAND(and.ID, operation.ID, index, operation.PathIndex, interpreterID);
                if (operation.IntoData != and.Data[operation.PathIndex])
                    throw new Exception("A");
                ++index;
            }
           
        }

        private int CreateFilters(FilterCollection filterCollection, int interpreterID)
        {
            if (filterCollection == null || filterCollection.Count == 0)
                return -1;

            int _filtersID = -1;
            _filtersID = CppConnector.CreateFilters(filterCollection.Count, interpreterID);
            if (_filtersID >= 0)
            {
                int index = 0;
                foreach (Filter f in filterCollection.Filters)
                    CppConnector.AddFilterToFilters(_filtersID, CreateFilter(f, interpreterID), index++, interpreterID);
                ++index;
            }
            return _filtersID;
        }

        private int CreateFilter(Filter f, int interpreterID)
        {
            int _mappingID = CreateMapping(f.FromToMapping, out f.Mapping, interpreterID);
            f.FromToMapping = null;
            int _filterID = CppConnector.CreateFilter(_mappingID, f.Source.ID, f.Distance, f.SourcePathIndex, interpreterID);
            return _filterID;
        }
        private void CreateOR_Operation(MergeOperation operation, int interpreterID)
        {
            int _newMappingID = -1;
            int _commonMappingID = -1;


            if(operation.NewMapping != null && operation.NewMapping.LocalColumns != null && operation.NewMapping.LocalColumns.Length > 0)
                _newMappingID = CppConnector.CreateMapping(operation.NewMapping.Length, operation.NewMapping.LocalColumns, operation.NewMapping.ForeignColumns, interpreterID);
            if (operation.CommonMapping != null && operation.CommonMapping.LocalColumns != null && operation.CommonMapping.LocalColumns.Length > 0)
                _commonMappingID = CppConnector.CreateMapping(operation.CommonMapping.Length, operation.CommonMapping.LocalColumns, operation.CommonMapping.ForeignColumns, interpreterID);

            int _distinctQueryID = -1;
            if (operation.DistinctQuery != null)
                _distinctQueryID = CppConnector.CreateQuery(operation.DistinctQuery, interpreterID);

          

            int operationBackColumnsLength = 0;
            if (operation.ForkBackColumns != null)
                operationBackColumnsLength = operation.ForkBackColumns.Length;


            int _filtersID = CreateFilters(operation.Filtering, interpreterID);

            
            int _fromNodeID = -1;
            if (operation.FromNode != null)
            {
                CreateOR(operation.FromNode, interpreterID);
                _fromNodeID = operation.FromNode.ID;
            }
            operation.ID = CppConnector.CreateMergeOperation(_fromNodeID, _newMappingID, _commonMappingID, operation.MergeType, operation.ForkType, operation.ForkIntoCount, operation.ForkBackColumns, operationBackColumnsLength, _distinctQueryID, _filtersID, operation.duplicateKiller.Arity, operation.duplicateKiller.LIMIT, interpreterID);
            if (operation.Recurr != null)
            {
                int _recurrenceFiltersID = CreateFilters(operation.Recurr.FilterstoPass, interpreterID);
                CppConnector.CreateRecurrence(operation.ID, operation.Recurr.ID, operation.Recurr.Condition.ConditionID, operation.Recurr.HashCode, _recurrenceFiltersID, interpreterID);
            }
        }
    }
}
