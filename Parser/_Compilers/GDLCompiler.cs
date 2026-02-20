using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Parser.Compilers;
using System.IO;
using System.Diagnostics;
using Parser.Simulator;
using Parser.Constraints;
using Parser._Compilers;

namespace Parser
{
	public partial class GDLCompiler
	{
		public const bool EnableCPP = false;
		int InterpreterID;
		int PlayersCount;

		CppIntermediateTranslator cpp = null;
		public Dictionary<string, NodeOR> FrameFacts = null;
		public Dictionary<string, NodeOR> ConstantFacts = null;

		NodeOR doesNode = null;
		NodeOR[] GoalByPlayer = null;
		NodeOR legalNode;
		int legalArity;
		NodeOR terminalNode;

		Dictionary<string, NodeOR> FrameNodes = null;

		public GDLCompilerRuleType CreatedRuleType;

		public Dictionary<int, RecurrenceByCondition> RecurrenceMapByCondition = null;
		public Dictionary<int, Recurrence> RecurrenceMapByID = null;

		NodeReuserManager ReuseMgr = new NodeReuserManager();
		GDLSimulator simulator = null;

		private readonly GDLRuleheet kb = null;
		public GDLCompiler(int identifier, GDLRuleheet knowledgeBase)
		{
			legalArity = knowledgeBase.Arity_Legal;
			kb = knowledgeBase;

			ConditionTextToID = new Dictionary<string, int>(knowledgeBase.ConditionTextToID);
			PlayersCount = knowledgeBase.PlayersCount;
			InterpreterID = identifier;
			cpp = new CppIntermediateTranslator();
			RecurrenceMapByCondition = new Dictionary<int, RecurrenceByCondition>();
			RecurrenceMapByID = new Dictionary<int, Recurrence>();
		}

		public void UpdateRecurrentNode(int recurrenceID, int mergeOperationID)
		{
			Recurrence r = null;
			if (RecurrenceMapByID.TryGetValue(recurrenceID, out r) == false)
				throw new Exception("TODO: impossible");

			NodeOR or = r.Expand();
			cpp.CreateOR(or, InterpreterID);
			CppConnector.SetFromInMergeOperation(mergeOperationID, or.ID, InterpreterID);
		}

		/*------------------------------------------- operations on data copied from RuleSheet ------------------*/
		public Dictionary<string, int> ConditionTextToID = new Dictionary<string, int>();
		public void UpdateConditionHash(GDLTerm condition)
		{
			string conditionTxt = condition.ToString();
			if (ConditionTextToID.TryGetValue(conditionTxt, out condition.HashID) == false)
			{
				condition.HashID = ConditionTextToID.Count;
				ConditionTextToID.Add(conditionTxt, condition.HashID);
			}
		}

		public delegate void DelegateRecurrence(int recurrenceID, int mergeOperationID);
		public DelegateRecurrence callBack = null;
		public GDLSimulator Compile()
		{
			simulator = new GDLSimulator(InterpreterID);
			if (EnableCPP)
			{
				callBack = new DelegateRecurrence(UpdateRecurrentNode);
				CppConnector.SetCallBack(callBack, InterpreterID);
				cpp.InitializeDictionaries(InterpreterID);
			}

			CreateFacts();
			CreateLegalTerminalGoal();
			CreateNEXT();
			ReuseMgr.RegisteringFinished();
			if (EnableCPP)
			{
				TranslateLegalTerminalGoal();
				TranslateNEXT();
				if (simulator.TerminalContainsLegal && InterpreterID == 0) /*Ustawienie optymalizacji*/
				{
					Console.WriteLine("Terminal rule contains legal");
					CppConnector.Simulator_SetTerminalContainsLegalOn(InterpreterID);
				}
			}
			/*--- create simulator, organize next */
			simulator.Init(PlayersCount, doesNode, legalNode, GoalByPlayer, terminalNode, FrameFacts.Count - 1, ReuseMgr);

			int index = 0;
			foreach (var entry in FrameFacts)
			{
				if (entry.Key != "does")
				{
					int factNodeID = entry.Value.ID;
					int ruleNodeID = -1;
					NodeOR ruleNode = null;
					if (FrameNodes.TryGetValue(entry.Key, out ruleNode))
					{
						ruleNodeID = ruleNode.ID;
					}

					simulator.NextFacts[index] = new FrameFact(ruleNode, entry.Value);
					simulator.SavedFacts[index] = new RowSet(entry.Value.Arity, entry.Value.Data.Capacity);
					if (EnableCPP)
					{
						CppConnector.Simulator_SetNext(factNodeID, ruleNodeID, index, InterpreterID);
					}
					++index;
				}
			}
			/*Last part - prepare reusability*/
			if (EnableCPP)
			{
				foreach (ReusableNodeOR reusableNode in ReuseMgr.DataFeed)
					CppConnector.CreateReusableNodeOR(reusableNode.ID, InterpreterID);

				foreach (var entry in ReuseMgr.ConsumerMap)
				{
					foreach (NodeOR node in entry.Value)
					{
						CppConnector.AddReusableLinkToOR(node.ID, node.ReuseLink.Node.ID, node.ReuseLink.ProducerConsumer, InterpreterID);
					}
				}
			}
			simulator.Restart();
			return simulator;
		}

		public void TranslateLegalTerminalGoal()
		{
			cpp.CreateOR(legalNode, InterpreterID);
			cpp.CreateOR(terminalNode, InterpreterID);

			for (int playerIndex = 0; playerIndex < PlayersCount; ++playerIndex)
			{
				cpp.CreateOR(GoalByPlayer[playerIndex], InterpreterID);
				CppConnector.Simulator_SetGoalByPlayer(GoalByPlayer[playerIndex].ID, playerIndex, InterpreterID);
			}

			CppConnector.Simulator_SetLegal(legalNode.ID, InterpreterID);
			CppConnector.Simulator_SetTerminal(terminalNode.ID, InterpreterID);
		}

		public void CreateLegalTerminalGoal()
		{
			CreatedRuleType = GDLCompilerRuleType.Legal;
			GoalByPlayer = new NodeOR[PlayersCount];
			int index = 0;
			foreach (var condition in kb.LegalConditions)
			{
				GoalByPlayer[index] = CreateOR("goal", kb.GoalConditions[index], null, null, false);
				++index;
			}

			legalNode = CreateOR("legal", null, null, null, false);
			CreatedRuleType = GDLCompilerRuleType.Terminal;
			terminalNode = CreateOR("terminal", null, null, null, false);
			CreatedRuleType = GDLCompilerRuleType.Goal;
		}

		public void CreateNEXT()
		{
			CreatedRuleType = GDLCompilerRuleType.Next;
			FrameNodes = new Dictionary<string, NodeOR>();
			foreach (var entry in kb.NextRules)
			{
				NodeOR nextNode = CreateOR(entry.Key, null, null, entry.Value, null, true, false);
				FrameNodes.Add(entry.Key, nextNode);
			}
		}

		public void TranslateNEXT()
		{
			CppConnector.Simulator_SetNextCount(FrameFacts.Count - 1, InterpreterID);
			foreach (var entry in FrameNodes)
			{
				cpp.CreateOR(entry.Value, InterpreterID);
			}
		}

		public void CreateFacts()
		{
			FrameFacts = new Dictionary<string, NodeOR>();
			ConstantFacts = new Dictionary<string, NodeOR>();

			doesNode = new NodeOR("does", null, legalArity, null);
			doesNode.Data = new RowSet(legalArity, PlayersCount);
			FrameFacts.Add("does", doesNode);

			if (EnableCPP)
			{
				cpp.CreateOR(doesNode, InterpreterID);
				CppConnector.Simulator_SetDoes(doesNode.ID, InterpreterID);
			}
			foreach (var entry in kb.ConstantFacts)
			{
				int arity = entry.Value.Terms.First().CurrentAriy;
				int count = entry.Value.Terms.Count;
				NodeOR or = new NodeOR(entry.Key, null, arity, null);
				or.Data = new RowSet(arity, count);
				ConstantFacts.Add(entry.Key, or);
				or.Data.FillFromTerms(entry.Value.Terms);
				or.Data.PerformHash();
				/* in c++: create the same NodeOR and add to constant facts*/
				if (EnableCPP)
					cpp.CreateOR(or, InterpreterID);

			}

			foreach (var entry in kb.InitFacts)
			{
				int arity = entry.Value.Terms.First().CurrentAriy;
				int count = entry.Value.Terms.Count;
				NodeOR or = new NodeOR(entry.Key, null, arity, null);
				or.Data = new RowSet(arity, count);
				or.Data.FillFromTerms(entry.Value.Terms);
				FrameFacts.Add(entry.Key, or);
			}

			foreach (var entry in kb.NextRules)
			{
				if (FrameFacts.ContainsKey(entry.Key) == false)
				{
					int arity = entry.Value.Rules.First().Header.CurrentAriy;
					NodeOR or = new NodeOR(entry.Key, null, arity, null);
					or.Data = new RowSet(arity, 2);
					FrameFacts.Add(entry.Key, or);
					/* in c++: create the same NodeOR and add to frame facts*/
				}
			}
			CreatedRuleType = GDLCompilerRuleType.Init;
			foreach (var entry in kb.InitRules)
			{
				NodeOR factNode = null;
				NodeOR initNode = CreateOR(entry.Key, null, null, entry.Value, null, true, false);
				if (initNode.Prove())
				{
					if (FrameFacts.TryGetValue(entry.Key, out factNode))
					{
						factNode.Data.MergeAdd(initNode.Data);
					}
					else
					{
						FrameFacts.Add(entry.Key, initNode);
					}
				}

			}
			if (EnableCPP)
			{
				foreach (var entry in FrameFacts)
					cpp.CreateOR(entry.Value, InterpreterID);
			}
		}
	}
}