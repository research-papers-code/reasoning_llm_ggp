using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser.Serialization
{
    public class StateSerializer
    {
        public byte[] Buffer = null;
        public int Count = 0;

        public StateSerializer()
        {
            Count = 0;
            Buffer = new byte[128];
        }

        public void AssertBuffer(int newSize)
        {
            if (Buffer.Length < newSize)
                Buffer = new byte[newSize];
        }
    }
}
