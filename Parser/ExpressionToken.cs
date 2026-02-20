using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
    /// <summary>
    /// Expression is any text contained within brackets e.g. (role x)
    /// </summary>
    public class ExpressionToken
    {
        private static char[] SPLITTER = new char[] { ' ' };
		private static char[] SPECIAL = new char[] { ' ', '(', ')', '}', '{', '\n', '\t', ']', '[', ';'};
        public string Text = "";
        public bool BracketContainer = false;
        public uint ID = 0;

        public ExpressionToken Parent = null;
        public List<ExpressionToken> Children = new List<ExpressionToken>();

        public static uint ID_SEED = 0;
        public ExpressionToken(ExpressionToken parent, bool bracketToken)
        {
            Parent = parent;
            if (parent != null)
                parent.Children.Add(this);
            ID = ID_SEED++;
            BracketContainer = bracketToken;
            
        }

        public void SetText(string text)
        {
            Text = text;
        }

        public override string ToString()
        {
            string txt = "";
            if (BracketContainer)
            {
                txt += "(";
                foreach (ExpressionToken token in Children)
                {
                    txt += " ";
                    txt += token.ToString();
                    txt += " ";
                }
                txt += ")";
            }
            else
                txt = Text;
            return txt;
        }

		public static short[][] TokenizeJointMove2(string logText)
		{
		
			string[] tokens = logText.Split(SPECIAL, StringSplitOptions.RemoveEmptyEntries);
			List<short>[] moves = new List<short>[Translator.RoleCount];
			for(int i=0; i < Translator.RoleCount;++i)
			{
				moves[i] = new List<short>();
			}

			short activeMove = -1;

			for(int i=0; i < tokens.Length; ++i)
			{
				short encoding = Translator.Instance.ToValue(tokens[i]);
				if(encoding >= 0 && encoding < Translator.RoleCount)
				{
					activeMove = encoding;
				}
				else
				{
					moves[activeMove].Add(encoding);
				}
		
			}

			short[][] returnMoves = new short[Translator.RoleCount][];
			for(int i=0; i < Translator.RoleCount; ++i)
			{
				returnMoves[i] = moves[i].ToArray();
			}

			return returnMoves;
		}

        public static short[][] TokenizeJointMove(string sentence)
        {
            if (sentence.Length >= 2)
            {
                if (sentence[0] == '(')
                    sentence = sentence.ToLower().Substring(1, sentence.Length - 2);
            }

            int openPars = 0;

            List<string> movesFound = new List<string>();
            string currentMove = "";
            foreach (char c in sentence)
            {
                if (c == '(')
                {
                    if (openPars == 0)
                    {
                        if (currentMove.Trim().Length > 0)
                        {
                            movesFound.Add(currentMove);
                            currentMove = "";
                        }
                    }
                    openPars++;
                }
                else if (c == ')')
                {
                    openPars--;
                }
                else if (Char.IsWhiteSpace(c))
                {
                    if (openPars == 0)
                    {
                        if (currentMove.Trim().Length > 0)
                        {
                            movesFound.Add(currentMove);
                            currentMove = "";
                        }
                    }
                    else
                        currentMove += c;
                }
                else
                {
                    currentMove += c;
                }
            }
            if (currentMove.Length > 0)
                movesFound.Add(currentMove);

            short[][] moves = new short[movesFound.Count][];
            int index = 0;

            foreach (string move in movesFound)
            {
                string[] tokens = move.Split(SPLITTER, StringSplitOptions.RemoveEmptyEntries);
                moves[index] = new short[tokens.Length];
                for (int i = 0; i < tokens.Length; ++i)
                    moves[index][i] = Translator.Instance.ToValue(tokens[i]);

                ++index;
            }
            return moves;
        }
        public void PrintToConsole()
        {
            if (BracketContainer)
            {
                Console.Write("(");
                foreach (ExpressionToken token in Children)
                {
                    Console.Write(" ");
                    token.PrintToConsole();
                    Console.Write(" ");
                }
                Console.Write(")");
            }
            else
                Console.Write(Text);
        }
    }
}
