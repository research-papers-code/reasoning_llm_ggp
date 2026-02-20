using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Parser
{
    public class GDLParser
    {
        private GDLRuleheet ruleSheet = null;
        public GDLRuleheet ParseFromFile(string filename, bool displayTime)
        {
            StringBuilder builder = new StringBuilder();
            StreamReader reader = new StreamReader(filename);
            string body = reader.ReadToEnd();
            reader.Close();

            bool removeMode = false;
            for (int i = 0; i < body.Length; ++i)
            {
                char c = body[i];
                if (removeMode)
                {
                    if (c == '\r' || c == '\n')
                        removeMode = false;
                }
                else
                {
                    if (c == ';')
                        removeMode = true;
                    else
                        builder.Append(c);
                }
            }

            return ParseFromString(builder.ToString(),displayTime);
        }

        public GDLRuleheet ParseFromMessage(string body)
        {
            bool decorationBrackets = (body.Length > 2 && body[0] == '(' && (body[1] == ' ' || body[1] == '(')); 
            StringBuilder builder = new StringBuilder();
            bool removeMode = false;
            for (int i = 0; i < body.Length; ++i)
            {
                char c = body[i];
                if (removeMode)
                {
                    if (c == '\r' || c == '\n')
                        removeMode = false;
                }
                else
                {
                    if (c == ';')
                        removeMode = true;
                    else
                        builder.Append(c);
                }
            }

            if (decorationBrackets)
                return ParseFromString(builder.ToString(1, builder.Length - 2), false);
            else
                return ParseFromString(builder.ToString(), false);
        }

        public GDLTerm CreateTerm(ExpressionToken token, bool orFound, bool notFound)
        {
            string name;
            if (token.BracketContainer)
                name = token.Children[0].Text;
            else
                name = token.Text;

            if (name.Equals("init"))
            {
                GDLTerm term = CreateTerm(token.Children[1], false, false);
                term.InitKeyword = true;
                return term;
            }
            else if (name.Equals("next"))
            {
                GDLTerm term = CreateTerm(token.Children[1], false, false);
                term.NextKeyword = true;
                return term;
            }
            else if (name.Equals("not"))
            {
                GDLTerm term = CreateTerm(token.Children[1], orFound, true);
                term.NOT = true;
                return term;
            }
            else if (name.Equals("true"))
            {
                GDLTerm term = CreateTerm(token.Children[1], orFound, notFound);
                term.TRUE = true;
                return term;
            }
            else if (name.Equals("or"))
            {
                GDLTerm term = new GDLTerm();
                term.OR = true;
                term.OrTerms = new List<GDLTerm>();
                for (int i = 1; i < token.Children.Count; ++i)
                {
                    GDLTerm childTerm = CreateTerm(token.Children[i], true, notFound);
                    term.OrTerms.Add(childTerm);
                }
                term.NOTOR = notFound;
                return term;
            }
            else
            {
                GDLTerm term = new GDLTerm();
                term.Name = name;
                if(name.Equals("distinct"))
                    term.DistinctKeyword = true;
                for (int i = 1; i < token.Children.Count; ++i)
                {
                    term.Arguments.Add(CreateVariable(token.Children[i], term));
                }
                return term;
            }
        }

        public GDLArgument CreateVariable(ExpressionToken token, GDLTerm parentTerm)
        {
            GDLArgument variable = new GDLArgument();
            if (token.Children.Count > 0)
            {
                variable.Children = new List<GDLArgument>();
                foreach (ExpressionToken child in token.Children)
                {
                    variable.Children.Add(CreateVariable(child,parentTerm));
                }
                variable.GlobalArity = variable.Arity;
            }
            else
            {
                variable.SetName(token.Text);
                variable.Constant = (token.Text[0] != '?');
            }
            return variable;
        }

        private GDLRuleheet ParseFromString(string rulesContent, bool displayTime)
        {
            ruleSheet = new GDLRuleheet();
            int openBrackets = 0;
            List<ExpressionToken> RootTokens = new List<ExpressionToken>();
            ExpressionToken currentBracketToken = null;
            StringBuilder builder = new StringBuilder();
            char lastC = ' ';
            for (int index = 0; index < rulesContent.Length; ++index)
            {
                char c = rulesContent[index];

                if (c == '(' && Char.IsWhiteSpace(lastC) == false)
                {
                    c = ' ';
                    --index;
                }

                if(c =='(')
                {
                    currentBracketToken = new ExpressionToken(currentBracketToken,true);
                    if(openBrackets == 0)
                        RootTokens.Add(currentBracketToken);
                    ++openBrackets;
                }
                else if(c == ')')
                {
                    if (builder.Length > 0)
                    {
                        ExpressionToken currentToken = new ExpressionToken(currentBracketToken,false);
                        currentToken.Text = builder.ToString();
                        builder.Clear();

                    }
                    --openBrackets;
                    currentBracketToken = currentBracketToken.Parent;
                }
                else if (Char.IsWhiteSpace(c))
                {
                    if (builder.Length > 0)
                    {
                        ExpressionToken currentToken = new ExpressionToken(currentBracketToken, false);
                        currentToken.Text = builder.ToString();
                        builder.Clear();
                    }
                }
                else
                {
                    builder.Append(c);
                }
                lastC = c;
            }

            foreach (ExpressionToken token in RootTokens)
            {
                string Name = token.Children[0].Text;
                if (Name.Equals("<="))
                {
                    GDLRule rule = new GDLRule();
                    bool addRule = true;
                    rule.Header = CreateTerm(token.Children[1], false, false);
                    for (int i = 2; i < token.Children.Count; ++i)
                    {
                        GDLTerm term = CreateTerm(token.Children[i], false, false);
                        if (term.NOTOR)
                        {
                            foreach (GDLTerm orTerm in term.OrTerms)
                            {
                                orTerm.NOT = !orTerm.NOT;
                                rule.AddCondition(orTerm);
                            }
                        }
                        else
                            rule.AddCondition(term);
                    }
                    if (rule.ContainedNOTDistinctInitially)
                    {
                        List<GDLTerm> notDistincts = rule.Conditions.Where(a => a.NOT && a.DistinctKeyword).ToList();
                        foreach (GDLTerm notDistinctCondition in notDistincts)
                        {
                            rule.Conditions.Remove(notDistinctCondition);
                            GDLArgument a1 = notDistinctCondition.Arguments[0];
                            GDLArgument a2 = notDistinctCondition.Arguments[1];
                            if (a1.Constant==false && a2.Constant==false)
                            {
                                rule.ReplaceArgument(a2.Name, a1.Name, new List<GDLTerm>());
                            }
                            else if (a1.Constant && a2.Constant==false)
                            {
                                rule.ReplaceArgument(a2.Name, a1.Name, new List<GDLTerm>());
                            }
                            else if (a1.Constant==false && a2.Constant)
                            {
                                rule.ReplaceArgument(a1.Name, a2.Name, new List<GDLTerm>());
                            }
                            else 
                            {
                                if (a1.Name != a2.Name)
                                {
                                    addRule = false;
                                    break;
                                }
                            }
                            
                        }
                    }
                    if(addRule)
                        ruleSheet.Add(rule);
                }
                else
                {
                    GDLTerm term = CreateTerm(token, false, false);
                    ruleSheet.Add(term);
                }
            }
            ruleSheet.OnFinished();
            
            return ruleSheet;
        }
    }
}
