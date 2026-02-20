using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser.Simulator
{
    public class SimulatorMoves
    {
        public static Random rand = new Random();
        public RowSet LegalData;
        public RowSet DoesData;
        public List<int> MoveStarts = new List<int>();

        public SimulatorMoves Clone()
        {
            SimulatorMoves copy = new SimulatorMoves(DoesData, new RowSet(LegalData.Arity, LegalData.Capacity));
            copy.MoveStarts.AddRange(MoveStarts);
            copy.LegalData.Rewrite(LegalData);
            return copy;
        }
        public SimulatorMoves(RowSet doesSet, RowSet legalSet)
        {
            DoesData = doesSet;
            LegalData = legalSet;
        }

        public override string ToString()
        {
            return string.Join(" ", MoveStarts);
        }

        public void ApplyRandom()
        {
            int moveNumber = rand.Next(MoveStarts.Count);
            int index = MoveStarts[moveNumber];
            int dataPtr = DoesData.Count*DoesData.Arity;
            for (int j = 0; j < DoesData.Arity; ++j)
                DoesData.Data[dataPtr++] = LegalData.Data[index++];

            ++DoesData.Count;
        }

        public void Apply(int moveNumber)
        {
            if(MoveStarts.Count == 0)
                throw new Exception("TODO: zero moves");
            if (moveNumber >= MoveStarts.Count)
                moveNumber %= MoveStarts.Count; //moveNumber = MoveStarts.Count - 1;
            
            int index = MoveStarts[moveNumber];
            int dataPtr = DoesData.Count * DoesData.Arity;
            for (int j = 0; j < DoesData.Arity; ++j)
                DoesData.Data[dataPtr++] = LegalData.Data[index++];
            ++DoesData.Count;
        }

        public short[] Extract(int moveNumber, short[] outMove)
        {
            int index = MoveStarts[moveNumber];
            for (int j = 0; j < DoesData.Arity; ++j)
                outMove[j] = LegalData.Data[index++];

            return outMove;
        }

        public void ExtractMove(int moveNumber, List<short> data)
        {
            int index = MoveStarts[moveNumber]+1;
            short temp;
            for (int j = 1; j < DoesData.Arity; ++j)
            {
                temp = LegalData.Data[index++];
                if (temp != Translator.BlankValue)
                    data.Add(temp);
            }
        }

        public string MoveToText(int moveNumber, bool includePlayer = false)
        {
            short[] move = new short[DoesData.Arity];
            int index = MoveStarts[moveNumber];
            int dataPtr = DoesData.Count * DoesData.Arity;
            for (int j = 0; j < DoesData.Arity; ++j)
                move[j] = LegalData.Data[index++];

            if (includePlayer)
            {
                return $"({Translator.Instance.ToSymbol(move[0])} {Translator.Instance.ActionToText(move)})";
            }
            else
            {
                return Translator.Instance.ActionToText(move);
            }
        }

        public int GetIndex(short[] move)
        {
            List<short> data = new List<short>();

			if (MoveStarts.Count == 1)
				return 0;

            for (int i = 0; i < MoveStarts.Count; ++i)
            {
                ExtractMove(i, data);
                int j = 0;
                if (data.Count == move.Length)
                {
                    for (j = 0; j < move.Length; ++j)
                    {
                        if (data[j] != move[j])
                            break;
                    }
                    if (j == move.Length)
                        return i;
                }
                data.Clear();
            }

			return -1;
        }

        public int Count
        {
            get { return MoveStarts.Count; }
        }
    }
}
