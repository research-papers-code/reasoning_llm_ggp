using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Parser
{
    public class ColumnHasher
    {
        private int[] Start;
        private int[] Stop;
        public ColumnHasher()
        {
            Start = new int[Translator.Dictionary.Count];
            Stop = new int[Translator.Dictionary.Count];
        }

        
        public void Clear()
        {
            for (int i = 0; i < Translator.Dictionary.Count; ++i)
            {
                Start[i] = int.MaxValue;
                Stop[i] = 0;
            }
        }

        public void Add(short symbol, int index)
        {
            if (Start[symbol] > index)
            {
                Start[symbol] = index;
                Stop[symbol] = index;
            }
            else
            {
                Stop[symbol] = index;
            }

        }

        public void Constrain(ref int start, ref int stop, short symbol)
        {
            start = Math.Max(start, Start[symbol]);
            stop = Math.Min(stop, Stop[symbol]);
        }

        public void ConstrainReverse(ref int start, ref int stop, short symbol)
        {
            start = Math.Min(start, Start[symbol]);
            stop = Math.Max(stop, Stop[symbol]);
        }
    }
}
