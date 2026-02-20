using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Parser.Simulator;

namespace Parser
{
    public class CppConnector
    {
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_Initialize(int playersCount, int threadsCount);

        public static int CreateRowSet(RowSet data, int interpreterID)
        {
            int returnID = CppConnector.CreateRowSet(data.ID, data.Arity, data.Capacity, interpreterID);
            CppConnector.CopyToRowset(data.ID, data.Data, data.Count, data.Hasher != null, interpreterID);
            return data.ID;
        }

        public static int CreateQuery(GDLQuery query, int interpreterID)
        {
            int id = CppConnector.CreateQuery(query.EqualSymbol.Count, query.EqualVariable.Count, interpreterID);
            for (int i = 0; i < query.EqualSymbol.Count; ++i)
            {
                GDLQuery_EqualVarSym es = query.EqualSymbol[i];
                CppConnector.AddEqSymToQuery(id, es.VarIndex, es.SymbolValue, i, interpreterID);
            }
            for (int i = 0; i < query.EqualVariable.Count; ++i)
            {
                GDLQuery_EqualVarVar ev = query.EqualVariable[i];
                CppConnector.AddEqVarToQuery(id, ev.Var1Index, ev.Var2Index, i, interpreterID);
            }
            return id;
        }
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_SetTerminalContainsLegalOn(int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void AddFilterToFilters(int filtersID, int filterID, int index, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void AddOperationToAND(int andNodeID, int operationID, int index, int pathIndex, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void AddFilterQueryToAND(int andNodeID, int filterID, int queryID, int index, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void AddRealizationToOR(int orNodeID, int realizationID, int index, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void AddEqSymToQuery(int queryID, int column, short symbol, int index, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void AddEqVarToQuery(int queryID, int column1, int column2, int index, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void CopyToRowset(int rowsetID, short[] data, int count, bool performHash, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern int CreateMapping(int length, int[] localColumns, int[] foreignColums, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern int CreateOR(int id, int nameValue, int parentID, int rowsetID, int groundNodeID, int orMode, int realizationsCount, int groundGDLQueryID, int filtersID, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void CreateReusableNodeOR(int id, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void AddReusableLinkToOR(int orID, int reusableNodeID, bool reuseProducerConsumer, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void CreateRecurrence(int mergeOperationID, int recurrenceID, int conditionID, int filtersHashCode, int filtersID, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern int CreateQuery(int equalSymCount, int equalVarCount, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern int CreateRowSet(int id, int arity, int initialCapacity, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern int CreateFilter(int mappingID, int rowsetID, int upDistance, int sourcePathIndex, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern int CreateFilters(int filtersCount, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern int CreateMergeOperation(int fromNodeID, int newMappingID, int commonMappingID, int mergeType, int forkType, int forkInCount, int[] forkBackColumns, int forkBackColumnsCount, int distinctQueryID, int filtersID, int arityDuplicates, int limitDuplicatesRuns, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern int CreateAND(int parentID, int operationsCount, int pathsCount, int retFilterQueriesCount, int mainRowsetID, int[] pathRowsetsID, int returnGDLQueryID, int arityDuplicates, int limitDuplicatesRuns, int interpreterID);

        [DllImport("GDLCompilerDLL.dll")]
        public static extern void InsertSymbol(short symbolValue, string symbolText, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void InsertFact(int symbolValue, string symbolText, int interpreterID);
       
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void ExtractSinglePlayerMoves(int[] moveIndices, int interpreterID);

        [DllImport("GDLCompilerDLL.dll")]
        public static extern int MonteCarlo(short[] goalValues, int timeMs, int interpreterID);

        [DllImport("GDLCompilerDLL.dll")]
        public static extern int MonteCarloSinglePlayer_Patterns(ref short goal, ref int moveCount, int startIndex, int indexIncrement, bool countFromStart,int timeMs, int interpreterID);

        [DllImport("GDLCompilerDLL.dll")]
        public static extern int MonteCarloSinglePlayer_Exploration(ref short goal, ref int moveCount, int explorationDepth, int explorationProbability, int timeMs, int interpreterID);

        public static int Rollout(GDLState state, short[] outGoalValues, int timeMs, int interpreterID)
        {
            foreach(FrameFact fact in state.NextFacts)
            {
                CppConnector.CopyToRowset(fact.DataNode.Data.ID, fact.DataNode.Data.Data, fact.DataNode.Data.Count, true, interpreterID);
            }
            return MonteCarlo(outGoalValues, timeMs, interpreterID);
        }

        public static int RolloutSinglePlayer_Patterns(GDLState state, short[] outGoalValues, double topScore, ref int moveCount, int startIndex, int indexIncrement, bool countFromStart, int timeMs, int interpreterID)
        {
            foreach (FrameFact fact in state.NextFacts)
            {
                CppConnector.CopyToRowset(fact.DataNode.Data.ID, fact.DataNode.Data.Data, fact.DataNode.Data.Count, true, interpreterID);
            }


            return MonteCarloSinglePlayer_Patterns(ref outGoalValues[0], ref moveCount, startIndex, indexIncrement, countFromStart, timeMs, interpreterID);
        }

        public static int RolloutSinglePlayer_Exploration(GDLState state, short[] outGoalValues, double topScore, ref int moveCount, int explorationDepth, int explorationFrequency, int timeMs, int interpreterID)
        {
            foreach (FrameFact fact in state.NextFacts)
            {
                CppConnector.CopyToRowset(fact.DataNode.Data.ID, fact.DataNode.Data.Data, fact.DataNode.Data.Count, true, interpreterID);
            }
            return MonteCarloSinglePlayer_Exploration(ref outGoalValues[0], ref moveCount, explorationDepth, explorationFrequency, timeMs, interpreterID);
        }
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_SetDoes(int nodeID, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_SetLegal(int nodeID, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_SetGoalByPlayer(int nodeID, int player, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_SetTerminal(int nodeID, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_SetGoal(int nodeID, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_SetNextCount(int count, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void Simulator_SetNext(int dataNodeID, int ruleNodeID, int index, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void SetFromInMergeOperation(int moID, int fromNodeID, int interpreterID);
        [DllImport("GDLCompilerDLL.dll", CharSet = CharSet.Auto)]
        public static extern void SetCallBack(MulticastDelegate callback, int interpreterID);
        [DllImport("GDLCompilerDLL.dll")]
        public static extern void SetDeterministicMove(int move, int interpreterID);
    }
}
