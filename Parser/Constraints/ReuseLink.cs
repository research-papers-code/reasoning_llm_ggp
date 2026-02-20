using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser.Constraints
{
    public class ReuseLink
    {
        public ReusableNodeOR Node = null;
        public bool ProducerConsumer = false;

        public override string ToString()
        {
            return ProducerConsumer.ToString();
        }
    }
}
