using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Diagnostics;


namespace Parser
{
    public unsafe class RowSet
    {
        public int ID;
        public static int ID_SEED = 0;
        public bool ActivePath;
        public int[] DeletedData;

        int DeletedCount;

        int FindRowPtr;
        public short[] Data;
        public int Count;

        public int Capacity;
        public int Arity;
        public int Stride;

        public GDLQuery GroundQuery = null;
        public GDLQuery DistinctQuery = null;
        public FilterCollection Filtering = null;

        public ColumnHasher[] Hasher = null;

        public string RowToString(int rowIndex)
        {
            StringBuilder builder = new StringBuilder();
            int index = rowIndex * Arity;

            for (int j = 0; j < Arity; ++j)
            {
				if (Data[index] != Translator.BlankValue)
				{
					string symbol = Translator.Instance.ToSymbol(Data[index++]);
					if (j > 0)
						builder.Append(" " + symbol);
					else
						builder.Append(symbol);
				}
				else
				{
					++index;
				}
            }

            return builder.ToString();
        }

        public string ToStringContent()
        {
            StringBuilder builder = new StringBuilder();
            int index = 0;
            for (int i = 0; i < Count; ++i)
            {
                builder.Append("[");
                for (int j = 0; j < Arity; ++j)
                {
                    string symbol = Translator.Instance.ToSymbol(Data[index++]);
                    if (j > 0)
                        builder.Append(" " + symbol);
                    else
                        builder.Append(symbol);
                }
                builder.Append("]");
            }
            return builder.ToString();
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("ID={0} C={1} D={2} ", ID, Count, DeletedCount);
            int index = 0;
            for (int i = 0; i < Count; ++i)
            {
                builder.Append("[");
                for (int j = 0; j < Arity; ++j)
                {
                    string symbol = Translator.Instance.ToSymbol(Data[index++]);
                    if (j > 0)
                        builder.Append(" " + symbol);
                    else
                        builder.Append(symbol);
                }
                builder.Append("]");
            }
            return builder.ToString();
        }

        public string ToMultilineString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("ID={0} Count={1} Deleted={2}", ID, Count, DeletedCount);
            builder.AppendLine();
            int index = 0;

            List<string> lines = new List<string>();
            for (int i = 0; i < Count; ++i)
            {
                string line = "[";
                for (int j = 0; j < Arity; ++j)
                {
                    string symbol = Translator.Instance.ToSymbol(Data[index++]);
                    if (j > 0)
                        line += (" " + symbol);
                    else
                        line += (symbol);
                }
                line += "]";
                lines.Add(line);
            }
            lines.Sort();
            foreach (string s in lines)
                builder.AppendLine(s);

            return builder.ToString();
        }

        public void F(string header)
        {
            StreamWriter writer = new StreamWriter("data.txt", true);
            writer.WriteLine(header);
            writer.WriteLine(ToMultilineString());
            writer.Flush();
            writer.Close();
        }
        public RowSet(int arity, int initialCapacity = 2)
        {
            ID = ID_SEED++;
            Count = 0;

            DeletedCount = 0;
            Arity = arity;
            Stride = Arity * sizeof(short);
            Data = null;
            FindRowPtr = -1;
            Reallocate(initialCapacity);
        }

        public void FillFromTerms(List<GDLTerm> list)
        {
            int i = 0;
            foreach (GDLTerm term in list)
            {
                foreach (var arg in term.Arguments)
                {
                    Data[i++] = Translator.Instance.ToValue(arg.Name);
                }
            }
            Count = list.Count;
        }

        public void Reallocate(int requestedCapacity)
        {
            if (requestedCapacity > Capacity)
            {
                if (requestedCapacity > 8)
                    requestedCapacity = (((requestedCapacity / 8) + (requestedCapacity % 8)) * 8);

                short[] newData = new short[requestedCapacity * Arity];
                int[] newDeletedData = new int[requestedCapacity * sizeof(int)];

                if (Data != null)
                {
                    if (Count > 0)
                        Array.Copy(Data, newData, Count * Arity);
                    if (DeletedCount > 0)
                        Array.Copy(DeletedData, newDeletedData, DeletedCount);
                }
                Data = newData;
                DeletedData = newDeletedData;
                Capacity = requestedCapacity;
               
            }
        }

        #region REWRITE

        /*--------------------------- REWRITE NO FILTER ------------------------------*/
        #region REWRITE_NO_FILTER
        public void Rewrite(RowSet from)
        {
            Clear();
            Reallocate(from.Count);
            if (Arity == from.Arity)
            {
                Buffer.BlockCopy(from.Data, 0, Data, 0, from.Count * from.Stride);
            }
            else
            {
                int dataPtr = 0;
                int fromPtr = 0;
                for (int i = 0; i < from.Count; ++i)
                {
                    for (int j = 0; j < Arity; ++j)
                        Data[dataPtr++] = from.Data[fromPtr + j];
                    fromPtr += from.Arity;
                }
            }
            Count = from.Count;
        }
        public void RewriteWithQuery(RowSet from, GDLQuery query)
        {
            Clear();
            Reallocate(from.Count);
            int dataPtr = 0;
            int fromPtr = 0;
            for (int i = 0; i < from.Count; ++i)
            {
                for (int j = 0; j < Arity; ++j)
                    Data[dataPtr + j] = from.Data[fromPtr + j];

                if (query.PassRow(this, dataPtr))
                {
                    dataPtr += Arity;
                    ++Count;
                }
                fromPtr += from.Arity;

            }
        }
        public void RewriteWithQuery_Hasher(RowSet from, GDLQuery query)
        {
            Clear();
            Reallocate(from.Count);
            int dataPtr = 0;
            int start = 0;
            int stop = from.Count-1;
            foreach (GDLQuery_EqualVarSym es in query.EqualSymbol)
                from.Hasher[es.VarIndex].Constrain(ref start, ref stop, es.SymbolValue);

            int fromPtr = start * from.Arity;
            for (int i = start; i <= stop; ++i)
            {
                for (int j = 0; j < Arity; ++j)
                    Data[dataPtr + j] = from.Data[fromPtr + j];

                if (query.PassRow(this, dataPtr))
                {
                    dataPtr += Arity;
                    ++Count;
                }

                fromPtr += from.Arity;

            }
        }
        #endregion

        
        #region REWRITE_FILTER_HASHER
        public void RewriteFilter_Hasher(RowSet from, Filter filter)
        {
            Clear();
            Reallocate(from.Count);
            int start = 0;
            int stop = from.Count - 1;
            int fromPtr = 0;

			if (stop < start)
				return;

			if (filter.Source.Count < 4 || from.Count > 15)
            {
                start = from.Count;
                stop = -1;
                for (int i = 0; i < filter.Source.Count; ++i)
                {
                    int a, b;
                    a = 0;
                    b = from.Count;
                    for (int j = 0; j < filter.Mapping.Length; ++j)
                        from.Hasher[filter.Mapping.LocalColumns[j]].Constrain(ref a, ref b, filter.Source.Data[fromPtr + filter.Mapping.ForeignColumns[j]]);

                    start = Math.Min(start, a);
                    stop = Math.Max(stop, b);
                    fromPtr += filter.Source.Arity;
                }
            }
            int dataPtr = 0;
            fromPtr = start * from.Arity;
            for (int i = start; i <= stop; ++i)
            {
                for (int j = 0; j < Arity; ++j)
                    Data[dataPtr + j] = from.Data[fromPtr + j];

                if (filter.PassRow(this, dataPtr))
                {
                    dataPtr += Arity;
                    ++Count;
                }
                fromPtr += from.Arity;

            }
        }

        public void RewriteFilterQuery_Hasher(RowSet from, GDLQuery query, Filter filter)
        {
            Clear();
            Reallocate(from.Count);
            int start = 0;
            int stop = from.Count - 1;
            int fromPtr = 0;

            if (filter.Source.Count < 4 || from.Count > 15)
            {
                start = from.Count;
                stop = -1;
                for (int i = 0; i < filter.Source.Count; ++i)
                {
                    int a, b;
                    a = 0;
                    b = from.Count;
                    for (int j = 0; j < filter.Mapping.Length; ++j)
                        from.Hasher[filter.Mapping.LocalColumns[j]].Constrain(ref a, ref b, filter.Source.Data[fromPtr + filter.Mapping.ForeignColumns[j]]);

                    start = Math.Min(start, a);
                    stop = Math.Max(stop, b);
                    fromPtr += filter.Source.Arity;
                }
            }

            foreach (GDLQuery_EqualVarSym es in query.EqualSymbol)
                from.Hasher[es.VarIndex].Constrain(ref start, ref stop, es.SymbolValue);

            int dataPtr = 0;
            fromPtr = start * from.Arity;
            for (int i = start; i <= stop; ++i)
            {
                for (int j = 0; j < Arity; ++j)
                    Data[dataPtr + j] = from.Data[fromPtr + j];

                if (query.PassRow(this, dataPtr) && filter.PassRow(this, dataPtr))
                {
                    dataPtr += Arity;
                    ++Count;
                }
                fromPtr += from.Arity;

            }
        }
        public void RewriteLearning_Hasher(RowSet from, GDLQuery query, Filter filter)
        {
            Clear();
            Reallocate(from.Count);
            int start = 0;
            int stop = from.Count - 1;
            int fromPtr = 0;
            if (filter.Source.Count < 4 && filter.Source.Count < from.Count)
            {
                start = from.Count;
                stop = -1;
                for (int i = 0; i < filter.Source.Count; ++i)
                {
                    int a, b;
                    a = 0;
                    b = from.Count;
                    for (int j = 0; j < filter.Mapping.Length; ++j)
                        from.Hasher[filter.Mapping.LocalColumns[j]].Constrain(ref a, ref b, filter.Source.Data[fromPtr + filter.Mapping.ForeignColumns[j]]);

                    start = Math.Min(start, a);
                    stop = Math.Max(stop, b);
                    fromPtr += filter.Source.Arity;
                }
            }
            if (start > 0 || stop < from.Count - 1)
                filter.Established = true;
            if (query != null)
            {
                foreach (GDLQuery_EqualVarSym es in query.EqualSymbol)
                    from.Hasher[es.VarIndex].Constrain(ref start, ref stop, es.SymbolValue);
            }
            int dataPtr = 0;
            fromPtr = start * from.Arity;

            for (int i = start; i <= stop; ++i)
            {
                for (int j = 0; j < Arity; ++j)
                    Data[dataPtr + j] = from.Data[fromPtr + j];

                if ((query == null || query.PassRow(this, dataPtr)) && filter.PassRow_L(this, dataPtr))
                {
                    dataPtr += Arity;
                    ++Count;
                }
                fromPtr += from.Arity;

            }
        }
        #endregion
      
        public void RewriteFilterQuery(RowSet from, GDLQuery query, Filter filter)
        {
            Clear();
            Reallocate(from.Count);
            int fromPtr = 0;
            int dataPtr = 0;
            if (query != null)
            {
                for (int i = 0; i < from.Count; ++i)
                {
                    for (int j = 0; j < Arity; ++j)
                        Data[dataPtr + j] = from.Data[fromPtr + j];

                    if (query.PassRow(this, dataPtr) && filter.PassRow(this, dataPtr))
                    {
                        dataPtr += Arity;
                        ++Count;
                    }
                    fromPtr += from.Arity;
                }
            }
            else
            {
                for (int i = 0; i < from.Count; ++i)
                {
                    for (int j = 0; j < Arity; ++j)
                        Data[dataPtr + j] = from.Data[fromPtr + j];

                    if (filter.PassRow(this, dataPtr))
                    {
                        dataPtr += Arity;
                        ++Count;
                    }
                    fromPtr += from.Arity;
                }
            }
        }
        #endregion

        public bool FilterOut()
        {
            int rowPtr = 0;
            for (int i = 0; i < Count; ++i)
            {
                foreach (Filter f in Filtering.Filters)
                {
                    if (f.PassRow(this, rowPtr) == false)
                    {
                        MarkDeleted(i);
                        break;
                    }
                }
                rowPtr += Arity;
            }
            if (DeletedCount == Count)
                return false;
            FinalizeDeletion();
            return Count > 0;
        }
        #region DELETING
        /******************************************************** DELETING ***********************/
        public int DeleteDuplicates(int[] columns)
        {
            if (Count < 2)
                return 0;

            int j;
            int mainRow = 0;
            int iteratorRow;

            for (int i = 0; i < Count; ++i)
            {
                iteratorRow = 0;
                for (int k = 0; k < i; ++k)
                {
                    j = 0;
                    for (j = 0; j < columns.Length; ++j)
                    {
                        if (Data[mainRow + columns[j]] != Data[iteratorRow + columns[j]])
                            break;
                    }
                    if (j == columns.Length)
                    {
                        MarkDeleted(i);
                        break;
                    }
                    iteratorRow += Arity;
                }
                mainRow += Arity;
            }

            int dupliCount = DeletedCount;
            if (DeletedCount > 0)
                FinalizeDeletion();
            return dupliCount;
        }


        public int DeleteDuplicates(int[] columns, int start)
        {
            if (Count < 2)
                return 0;

            int j;
            int mainRow = start * Arity;
            int iteratorRow;

            for (int i = start; i < Count; ++i)
            {
                iteratorRow = 0;
                for (int k = 0; k < i; ++k)
                {
                    j = 0;
                    for (j = 0; j < columns.Length; ++j)
                    {
                        if (Data[mainRow + columns[j]] != Data[iteratorRow + columns[j]])
                            break;
                    }
                    if (j == columns.Length)
                    {
                        MarkDeleted(i);
                        break;
                    }
                    iteratorRow += Arity;
                }
                mainRow += Arity;
            }

            int dupliCount = DeletedCount;
            if (DeletedCount > 0)
                FinalizeDeletion();
            return dupliCount;
        }

        public int DeleteDuplicates(int start, int arityMax)
        {
            if (Count < 2)
                return 0;

            int j;
            int mainRow = start * Arity;
            int iteratorRow;

            for (int i = start; i < Count; ++i)
            {
                iteratorRow = 0;
                for (int k = 0; k < i; ++k)
                {
                    for (j = 0; j < arityMax; ++j)
                    {
                        if (Data[mainRow + j] != Data[iteratorRow + j])
                            break;
                    }
                    if (j == arityMax)
                    {
                        MarkDeleted(i);
                        break;
                    }
                    iteratorRow += Arity;
                }
                mainRow += Arity;
            }

            int dupliCount = DeletedCount;
            if (DeletedCount > 0)
                FinalizeDeletion();
            return dupliCount;
        }

        public void Clear()
        {
            Count = 0;
            DeletedCount = 0;
        }

        public void MarkDeleted(int index)
        {
            DeletedData[DeletedCount++] = index;
        }

        public int FinalizeDeletion()
        {
            int lowest = Count;
            for (int i = DeletedCount - 1; i >= 0; --i)
            {
                lowest = DeletedData[i];
                if (lowest != Count - 1)
                {
                    for (int j = 0; j < Arity; ++j)
                        Data[j + lowest * Arity] = Data[j + (Count - 1) * Arity];
                }
                --Count;
            }
            DeletedCount = 0;
            return lowest;
        }
        #endregion
        public int FinalizeDeletion(MergeOperation op)
        {
            int lowest = Count;
            int last = (Count - 1) * Arity;
            for (int i = DeletedCount - 1; i >= 0; --i)
            {
                lowest = DeletedData[i];
                if (lowest != Count - 1)
                {
                    for (int j = 0; j < op.ForkBackColumns.Length; ++j)
                        Data[op.ForkBackColumns[j] + lowest * Arity] = Data[op.ForkBackColumns[j] + last];
                }
                --Count;
                last -= Arity;
            }
            DeletedCount = 0;
            return lowest;
        }

        public int FindNextRow(int startRowIndex, GDLMapping srcThisMapping, short[] srcData, int srcRowPtr)
        {
            if (Hasher != null && Count > 4)
            {
                return FindNextRowWithHasher(startRowIndex, srcThisMapping, srcData, srcRowPtr);
            }
            FindRowPtr = startRowIndex * Arity;
            int j;
            for (int i = startRowIndex; i < Count; ++i)
            {
                for (j = 0; j < srcThisMapping.Length; ++j)
                {
                    if (Data[FindRowPtr + srcThisMapping.ForeignColumns[j]] != srcData[srcRowPtr + srcThisMapping.LocalColumns[j]])
                        break;
                }
                if (j == srcThisMapping.Length)
                    return i;

                FindRowPtr += Arity;
            }
            return -1;
        }
        public int FindNextRow(int startIndex, int stopIndex, GDLMapping srcThisMapping, short[] srcData, int srcRowPtr)
        {
            if (Hasher != null && Count > 4)
            {
                return FindNextRowWithHasher(startIndex, stopIndex, srcThisMapping, srcData, srcRowPtr);
            }
            FindRowPtr = startIndex * Arity;
            int j;
            for (int i = startIndex; i <= stopIndex; ++i)
            {
                for (j = 0; j < srcThisMapping.Length; ++j)
                {
                    if (Data[FindRowPtr + srcThisMapping.ForeignColumns[j]] != srcData[srcRowPtr + srcThisMapping.LocalColumns[j]])
                        break;
                }
                if (j == srcThisMapping.Length)
                    return i;

                FindRowPtr += Arity;
            }
            return -1;
        }

        public int FindNextRowWithHasher(int startRowIndex, GDLMapping srcThisMapping, short[] srcData, int srcRowPtr)
        {
            int start = startRowIndex;
            int stop = Count;

            int j;
            for (j = 0; j < srcThisMapping.Length; ++j)
            {
                Hasher[srcThisMapping.ForeignColumns[j]].Constrain(ref start, ref stop, srcData[srcRowPtr + srcThisMapping.LocalColumns[j]]);
                if (start > stop)
                    return -1;
            }
            FindRowPtr = start * Arity;
            for (int i = start; i <= stop; ++i)
            {
                for (j = 0; j < srcThisMapping.Length; ++j)
                {
                    if (Data[FindRowPtr + srcThisMapping.ForeignColumns[j]] != srcData[srcRowPtr + srcThisMapping.LocalColumns[j]])
                        break;
                }
                if (j == srcThisMapping.Length)
                    return i;

                FindRowPtr += Arity;
            }
            return -1;
        }

        public int FindNextRowWithHasher(int startRowIndex, int stopIndex, GDLMapping srcThisMapping, short[] srcData, int srcRowPtr)
        {
            int start = startRowIndex;
            int stop = stopIndex;

            int j;
            for (j = 0; j < srcThisMapping.Length; ++j)
            {
                Hasher[srcThisMapping.ForeignColumns[j]].Constrain(ref start, ref stop, srcData[srcRowPtr + srcThisMapping.LocalColumns[j]]);
                if (start > stop)
                    return -1;
            }
            FindRowPtr = start * Arity;
            for (int i = start; i <= stop; ++i)
            {
                for (j = 0; j < srcThisMapping.Length; ++j)
                {
                    if (Data[FindRowPtr + srcThisMapping.ForeignColumns[j]] != srcData[srcRowPtr + srcThisMapping.LocalColumns[j]])
                        break;
                }
                if (j == srcThisMapping.Length)
                    return i;

                FindRowPtr += Arity;
            }
            return -1;
        }

        public void MergeForBack(RowSet from, int[] backColumns)
        {
            Reallocate(Count + from.Count);
            int fromPtr = 0;
            int dataPtr = Count * Arity;
            int j;

            for (int i = 0; i < from.Count; ++i)
            {
                for (j = 0; j < backColumns.Length; ++j)
                    Data[dataPtr + backColumns[j]] = from.Data[fromPtr + backColumns[j]];

                fromPtr += from.Arity;
                dataPtr += Arity;
                ++Count;
            }
        }

        public void MergeAdd(RowSet from)
        {
            Reallocate(Count + from.Count);
            if (from.Arity == Arity)
            {
                Buffer.BlockCopy(from.Data, 0, Data, Count * Stride, from.Count * from.Stride);
                Count += from.Count;
            }
            else
            {
                int a = Math.Min(Arity, from.Arity);
                int fromPtr = 0;
                int dataPtr = Count * Arity;
                int j;
                for (int i = 0; i < from.Count; ++i)
                {
                    for (j = 0; j < a; ++j)
                        Data[dataPtr + j] = from.Data[fromPtr + j];

                    fromPtr += from.Arity;
                    dataPtr += Arity;
                    ++Count;
                }
            }
        }

        public void CloneRowNoAdvance(int rowIndex)
        {
            if (Count == Capacity)
                Reallocate(Count + 1);

            int clonedPtr = rowIndex * Arity;
            int dataPtr = Count * Arity;
            for (int j = 0; j < Arity; ++j)
                Data[dataPtr + j] = Data[clonedPtr + j];
        }

        public void CloneCopyX(int totalClones)
        {
            Reallocate(Count * totalClones);

            int elements = Count * Arity;
            int dataPtr = elements;
            for (int i = 0; i < totalClones - 1; ++i)
            {
                Buffer.BlockCopy(Data, 0, Data, dataPtr * sizeof(short), elements * sizeof(short));
                dataPtr += elements;
            }
            Count *= totalClones;
        }

        /*----------------------------------------------------------------------------------- PASS ROW OPERATIONS -----------------*/
        public bool PassRow_Learning(int rowPtr)
        {
            if (DistinctQuery != null && DistinctQuery.PassRowInverse(this, rowPtr) == false)
                return false;

            if (Filtering != null)
            {
                foreach (Filter f in Filtering.Filters)
                {
                    if (f.PassRow_L(this, rowPtr) == false)
                    {
            
                        return false;
                    }
                }
            }
            return true;
        }


        #region Regular_MergeOperations
        public bool MergeNew(GDLMapping newMapping, RowSet other)
        {
            Reallocate(other.Count);
            int index = 0;
            int DataPtr = 0;
            for (int i = 0; i < other.Count; ++i)
            {
                for (int j = 0; j < newMapping.Length; ++j)
                {
                    Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[index + newMapping.ForeignColumns[j]];
                }
                index += other.Arity;
                DataPtr += Arity;
                ++Count;
            }
            return (Count > 0);
        }

        public bool MergeTrue(GDLMapping newMapping, GDLMapping commonMapping, RowSet other)
        {
            int otherIndex = 0;
            int curRow = 0;
            bool returnValue = false;
            int baseCount = Count;

            int DataPtr = Count * Arity;
            for (int i = 0; i < baseCount; ++i)
            {
                otherIndex = other.FindNextRow(0, commonMapping, Data, curRow);

                if (otherIndex < 0)
                    MarkDeleted(i);
                else
                {
                    returnValue = true;
                    for (int j = 0; j < newMapping.Length; ++j)
                        Data[curRow + newMapping.LocalColumns[j]] = other.Data[other.FindRowPtr + newMapping.ForeignColumns[j]];
                }
                while (otherIndex >= 0)
                {
                    otherIndex = other.FindNextRow(otherIndex + 1, commonMapping, Data, curRow);
                    if (otherIndex > 0)
                    {
                        Reallocate(Count + 1);
                        curRow = (i * Arity);
                        for (int j = 0; j < Arity; ++j)
                            Data[DataPtr + j] = Data[curRow + j];

                        for (int j = 0; j < newMapping.Length; ++j)
                            Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[other.FindRowPtr + newMapping.ForeignColumns[j]];

                        DataPtr += Arity;
                        ++Count;
                    }
                }
                curRow += Arity;
            }
            return returnValue;
        }

        public bool MergeCombine(GDLMapping newMapping, RowSet other)
        {
            int oldCount = Count;
            if (other.Count == 0 || oldCount == 0)
                return false;

            int otherPtr = 0;
            if (other.Count > 1)
            {
                CloneCopyX(other.Count);
                int curPtr = 0;
                if (oldCount == 1) /*(1 X > 1)*/
                {
                    for (int k = 0; k < other.Count; ++k)
                    {
                        for (int j = 0; j < newMapping.Length; ++j)
                            Data[curPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                        curPtr += Arity;
                        otherPtr += other.Arity;
                    }
                }
                else /*(> 1 X > 1)*/
                {
                    for (int i = 0; i < oldCount; ++i)
                    {

                        for (int j = 0; j < newMapping.Length; ++j)
                            Data[curPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];
                        curPtr += Arity;
                    }

                    for (int k = 1; k < other.Count; ++k)
                    {

                        otherPtr += other.Arity;
                        for (int i = 0; i < oldCount; ++i)
                        {


                            for (int j = 0; j < newMapping.Length; ++j)
                                Data[curPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];
                            curPtr += Arity;
                        }
                    }
                }
            }
            else if (oldCount > 1) /* (> 1 X 1)*/
            {
                int curRow = 0;
                for (int i = 0; i < Count; ++i)
                {
                    for (int j = 0; j < newMapping.Length; ++j)
                        Data[curRow + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                    curRow += Arity;
                }
            }
            else /* 1 x 1*/
            {
                for (int j = 0; j < newMapping.Length; ++j)
                    Data[newMapping.LocalColumns[j]] = other.Data[newMapping.ForeignColumns[j]];
            }

            return true;
        }

        public bool MergeVerify(GDLMapping commonMapping, RowSet other)
        {
            if (commonMapping == null)
            {
                return (other.Count > 0 || other.Arity == 0);
            }
            int curRow = 0;
            for (int i = 0; i < Count; ++i)
            {
                if (other.FindNextRow(0, commonMapping, Data, curRow) < 0)
                    MarkDeleted(i);
                curRow += Arity;
            }
            return (DeletedCount < Count);
        }

        public bool MergeNOT(GDLMapping commonMapping, RowSet other, bool result)
        {
            if (result == false)
                return true;
            if (commonMapping == null)
            {
                return !result;
            }
            int curRow = 0;
            for (int i = 0; i < Count; ++i)
            {
                if (other.FindNextRow(0, commonMapping, Data, curRow) >= 0)
                    MarkDeleted(i);
                curRow += Arity;
            }
            return (DeletedCount < Count);
        }
        #endregion

        #region EarlyExit_MergeOperations
        public bool MergeNew_EE(GDLMapping newMapping, RowSet other)
        {
            if (other.Count == 0)
                return false;
            Reallocate(1);
            for (int i = 0; i < 1; ++i)
            {
                for (int j = 0; j < newMapping.Length; ++j)
                {
                    Data[newMapping.LocalColumns[j]] = other.Data[newMapping.ForeignColumns[j]];
                }
                ++Count;
            }
            return true;
        }

        public bool MergeTrue_EE(GDLMapping newMapping, GDLMapping commonMapping, RowSet other)
        {
            int curRow = 0;
            for (int i = 0; i < Count; ++i)
            {
                if (other.FindNextRow(0, commonMapping, Data, curRow) >= 0)
                {
                    Count = 1;
                    return true;
                }
                curRow += Arity;
            }
            return false;
        }

        public bool MergeCombine_EE(GDLMapping newMapping, RowSet other)
        {
            return (other.Count > 0 && Count > 0);
        }

        public bool MergeVerify_EE(GDLMapping commonMapping, RowSet other)
        {
            if (commonMapping == null)
            {
                return (other.Count > 0 || other.Arity == 0);
            }
            int curRow = 0;
            for (int i = 0; i < Count; ++i)
            {
                if (other.FindNextRow(0, commonMapping, Data, curRow) >= 0)
                {
                    Count = 1;
                    return true;
                }
                curRow += Arity;
            }
            return false;
        }

        public bool MergeNOT_EE(GDLMapping commonMapping, RowSet other, bool result)
        {
            if (commonMapping == null)
            {
                return !result;
            }
            int curRow = 0;
            for (int i = 0; i < Count; ++i)
            {
                if (other.FindNextRow(0, commonMapping, Data, curRow) < 0)
                {
                    Count = 1;
                    return true;
                }
                curRow += Arity;
            }
            return false;
        }
        #endregion


        public bool MergeNew_FQL(GDLMapping newMapping, RowSet other)
        {
            Reallocate(other.Count);
            int index = 0;
            int DataPtr = 0;
            for (int i = 0; i < other.Count; ++i)
            {
                for (int j = 0; j < newMapping.Length; ++j)
                    Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[index + newMapping.ForeignColumns[j]];

                if (PassRow_Learning(DataPtr))
                {
                    DataPtr += Arity;
                    ++Count;
                }
                index += other.Arity;
            }
            return (Count > 0);
        }

        public bool MergeTrue_FQL(GDLMapping newMapping, GDLMapping commonMapping, RowSet other)
        {
            int otherIndex = 0;
            int curRow = 0;
            int baseCount = Count;
            int DataPtr = Count * Arity;
            for (int i = 0; i < baseCount; ++i)
            {
                otherIndex = other.FindNextRow(0, commonMapping, Data, curRow);
                if (otherIndex < 0)
                    MarkDeleted(i);
                else
                {
                    for (int j = 0; j < newMapping.Length; ++j)
                        Data[curRow + newMapping.LocalColumns[j]] = other.Data[other.FindRowPtr + newMapping.ForeignColumns[j]];

                    while (otherIndex >= 0)
                    {
                        otherIndex = other.FindNextRow(otherIndex + 1, commonMapping, Data, curRow);
                        if (otherIndex > 0)
                        {
                            Reallocate(Count + 1);
                            curRow = (i * Arity);
                            for (int j = 0; j < Arity; ++j)
                                Data[DataPtr + j] = Data[curRow + j];
                            for (int j = 0; j < newMapping.Length; ++j)
                                Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[other.FindRowPtr + newMapping.ForeignColumns[j]];

                            if (PassRow_Learning(DataPtr))
                            {
                                DataPtr += Arity;
                                ++Count;
                            }
                        }
                    }
                    if (PassRow_Learning(curRow) == false)
                        MarkDeleted(i);
                }
                curRow += Arity;
            }
            return (DeletedCount < Count);
        }

        public bool MergeCombine_FQL(GDLMapping newMapping, RowSet other)
        {
            int oldCount = Count;
            if (other.Count == 0 || oldCount == 0)
                return false;

            int otherPtr = 0;
            int DataPtr = Count * Arity;
            if (other.Count > 1)
            {
                int curPtr = 0;
                if (oldCount == 1) /*(1 X > 1)*/
                {
                    Reallocate(other.Count);
                    for (int j = 0; j < newMapping.Length; ++j)
                        Data[newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                    if (PassRow_Learning(0) == false)
                        MarkDeleted(0);
                    otherPtr += other.Arity;
                    for (int k = 1; k < other.Count; ++k)
                    {
                        CloneRowNoAdvance(0);
                        for (int j = 0; j < newMapping.Length; ++j)
                            Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                        if (PassRow_Learning(DataPtr))
                        {
                            DataPtr += Arity;
                            ++Count;
                        }
                        otherPtr += other.Arity;
                    }
                }
                else /*(> 1 X > 1)*/
                {
                    for (int i = 0; i < oldCount; ++i)
                    {
                        for (int j = 0; j < newMapping.Length; ++j)
                            Data[curPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                        if (PassRow_Learning(curPtr) == false)
                            MarkDeleted(i);
                        curPtr += Arity;
                    }

                    for (int k = 1; k < other.Count; ++k)
                    {
                        otherPtr += other.Arity;
                        for (int i = 0; i < oldCount; ++i)
                        {
                            CloneRowNoAdvance(i);
                            for (int j = 0; j < newMapping.Length; ++j)
                                Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                            if (PassRow_Learning(DataPtr))
                            {
                                DataPtr += Arity;
                                ++Count;
                            }
                        }
                    }
                }
            }
            else if (oldCount > 1) /* (> 1 X 1)*/
            {
                int curRow = 0;
                for (int i = 0; i < Count; ++i)
                {
                    for (int j = 0; j < newMapping.Length; ++j)
                        Data[curRow + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                    if (PassRow_Learning(curRow) == false)
                        MarkDeleted(i);

                    curRow += Arity;
                }
            }
            else /* 1 x 1*/
            {
                for (int j = 0; j < newMapping.Length; ++j)
                    Data[newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                return PassRow_Learning(0);
            }

            return (DeletedCount < Count);
        }

        public bool MergeNew_Q(GDLMapping newMapping, RowSet other)
        {
            Reallocate(other.Count);
            int index = 0;
            int DataPtr = 0;
            for (int i = 0; i < other.Count; ++i)
            {
                for (int j = 0; j < newMapping.Length; ++j)
                    Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[index + newMapping.ForeignColumns[j]];

                if (DistinctQuery.PassRowInverse(this, DataPtr))
                {
                    DataPtr += Arity;
                    ++Count;
                }
                index += other.Arity;
            }
            return (Count > 0);
        }

        public bool MergeTrue_Q(GDLMapping newMapping, GDLMapping commonMapping, RowSet other)
        {
            int otherIndex = 0;
            int curRow = 0;
            int baseCount = Count;
            int DataPtr = Count * Arity;
            for (int i = 0; i < baseCount; ++i)
            {
                otherIndex = other.FindNextRow(0, commonMapping, Data, curRow);
                if (otherIndex < 0)
                    MarkDeleted(i);
                else
                {
                    for (int j = 0; j < newMapping.Length; ++j)
                        Data[curRow + newMapping.LocalColumns[j]] = other.Data[other.FindRowPtr + newMapping.ForeignColumns[j]];

                    while (otherIndex >= 0)
                    {
                        otherIndex = other.FindNextRow(otherIndex + 1, commonMapping, Data, curRow);
                        if (otherIndex > 0)
                        {
                            Reallocate(Count + 1);
                            curRow = (i * Arity);
                            for (int j = 0; j < Arity; ++j)
                                Data[DataPtr + j] = Data[curRow + j];
                            for (int j = 0; j < newMapping.Length; ++j)
                                Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[other.FindRowPtr + newMapping.ForeignColumns[j]];

                            if (DistinctQuery.PassRowInverse(this, DataPtr))
                            {
                                DataPtr += Arity;
                                ++Count;
                            }
                        }
                    }
                    if (DistinctQuery.PassRowInverse(this, curRow) == false)
                        MarkDeleted(i);
                }
                curRow += Arity;
            }
            return (DeletedCount < Count);
        }

        public bool MergeCombine_Q(GDLMapping newMapping, RowSet other)
        {
            int oldCount = Count;
            if (other.Count == 0 || oldCount == 0)
                return false;

            int otherPtr = 0;
            int DataPtr = Count * Arity;
            if (other.Count > 1)
            {
                int curPtr = 0;
                if (oldCount == 1) /*(1 X > 1)*/
                {
                    Reallocate(other.Count);
                    for (int j = 0; j < newMapping.Length; ++j)
                        Data[newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                    if (DistinctQuery.PassRowInverse(this, 0) == false)
                        MarkDeleted(0);
                    otherPtr += other.Arity;
                    for (int k = 1; k < other.Count; ++k)
                    {
                        CloneRowNoAdvance(0);
                        for (int j = 0; j < newMapping.Length; ++j)
                            Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                        if (DistinctQuery.PassRowInverse(this, DataPtr))
                        {
                            DataPtr += Arity;
                            ++Count;
                        }
                        otherPtr += other.Arity;
                    }
                }
                else /*(> 1 X > 1)*/
                {
                    for (int i = 0; i < oldCount; ++i)
                    {
                        for (int j = 0; j < newMapping.Length; ++j)
                            Data[curPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                        if (DistinctQuery.PassRowInverse(this, curPtr) == false)
                            MarkDeleted(i);
                        curPtr += Arity;
                    }

                    for (int k = 1; k < other.Count; ++k)
                    {
                        otherPtr += other.Arity;
                        for (int i = 0; i < oldCount; ++i)
                        {
                            CloneRowNoAdvance(i);
                            for (int j = 0; j < newMapping.Length; ++j)
                                Data[DataPtr + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                            if (DistinctQuery.PassRowInverse(this, DataPtr))
                            {
                                DataPtr += Arity;
                                ++Count;
                            }
                        }
                    }
                }
            }
            else if (oldCount > 1) /* (> 1 X 1)*/
            {
                int curRow = 0;
                for (int i = 0; i < Count; ++i)
                {
                    for (int j = 0; j < newMapping.Length; ++j)
                        Data[curRow + newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                    if (DistinctQuery.PassRowInverse(this, curRow) == false)
                        MarkDeleted(i);

                    curRow += Arity;
                }
            }
            else /* 1 x 1*/
            {
                for (int j = 0; j < newMapping.Length; ++j)
                    Data[newMapping.LocalColumns[j]] = other.Data[otherPtr + newMapping.ForeignColumns[j]];

                return DistinctQuery.PassRowInverse(this, 0);
            }

            return (DeletedCount < Count);
        }

        public void CreateHasher()
        {
            Hasher = new ColumnHasher[Arity];
            for (int i = 0; i < Arity; ++i)
                Hasher[i] = new ColumnHasher();
        }

        internal unsafe void PerformHash()
        {
            if (Hasher == null)
                CreateHasher();
            int fromPtr = 0;
            for (int j = 0; j < Arity; ++j)
                Hasher[j].Clear();

            for (int i = 0; i < Count; ++i)
            {
                for (int j = 0; j < Arity; ++j)
                {
                    Hasher[j].Add(Data[fromPtr++], i);
                }
            }
        }

        internal unsafe void RewriteHash(RowSet from)
        {
            if (Hasher == null)
                CreateHasher();
            Clear();
            Reallocate(from.Count);
            int dataPtr = 0;
            int fromPtr = 0;
            for (int j = 0; j < Arity; ++j)
                Hasher[j].Clear();

            for (int i = 0; i < from.Count; ++i)
            {
                for (int j = 0; j < Arity; ++j)
                {
                    Data[dataPtr] = from.Data[fromPtr + j];
                    Hasher[j].Add(Data[dataPtr], i);
                    ++dataPtr;
                }
                fromPtr += from.Arity;
            }

            Count = from.Count;
        }

        public void ForcedInsert(short[][] correctOrderedMoves)
        {
            throw new NotImplementedException();
        }

        public RowSet Copy()
        {
            RowSet set = new RowSet(Arity, Capacity);
            set.Rewrite(this);
            return set;
        }
    }
}
