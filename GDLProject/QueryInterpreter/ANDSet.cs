using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QueryInterpreter.Tokenization;

namespace QueryInterpreter
{
    /*
    public class ANDSet
    {
        public ANDSet(Token token)
        {
            Construct1(token);
        }

        private void Construct1(Token token)
        {
            
        }

        private void Test()
        {
            RowSet set = new RowSet(2, 16);

            RowSet set1 = new RowSet(2, 16);

            Translator.Instance.InsertSymbol("0");
            Translator.Instance.InsertSymbol("1");
            Translator.Instance.InsertSymbol("2");
            Translator.Instance.InsertSymbol("3");
            Translator.Instance.InsertSymbol("4");
          
            set.Debug_Put("0 4");

            set1.Debug_Put("1 1");
            set1.Debug_Put("2 1");
            set1.Debug_Put("3 1");

            set1.Debug_Put("1 2");
            set1.Debug_Put("2 2");
            set1.Debug_Put("3 2");

            set1.Debug_Put("1 3");
            set1.Debug_Put("2 3");
            set1.Debug_Put("3 4");

            MergeOperation op1 = new MergeOperation();
            op1.Type = MergeOperationType.TrueMerge;

            op1.NewMapping = new Mapping();
            op1.NewMapping.Data = new Index2[1] { new Index2()};
            op1.NewMapping.Data[0].First = 0;
            op1.NewMapping.Data[0].Last = 0;

            op1.CommonMapping = new Mapping();
            op1.CommonMapping.Data = new Index2[1] { new Index2() };
            op1.CommonMapping.Data[0].First = 1;
            op1.CommonMapping.Data[0].Last = 1;

            op1.RuleDefinitions = new ORSet();
            op1.RuleDefinitions.Set = set1;

            set.Merge(op1);
        }
    }*/
}
