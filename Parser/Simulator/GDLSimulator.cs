using System;
using System.Collections.Generic;
using System.Text;
using Parser.Constraints;

namespace Parser.Simulator
{
	public class GDLSimulator
    {
        public int InterpreterID;
        public static int PlayersCount = 0;
      
        private Random random = new Random();
        private NodeOR DoesNode = null;
        private NodeOR LegalNode = null;
        private NodeOR[] GoalByPlayer = null;
        public NodeOR TerminalNode = null;

        public int CppErrorsCount = 0;
        public RowSet[] SavedFacts = null;

        private void SaveState()
        {
            for (int i = 0; i < CurrentState.NextFacts.Length; ++i)
                SavedFacts[i].Rewrite(NextFacts[i].DataNode.Data);
        }

        /// <summary>
        /// Loads state from the saved one via SaveState().
        /// </summary>
        private void LoadState()
        {
            for (int i = 0; i < CurrentState.NextFacts.Length; ++i)
                NextFacts[i].DataNode.Data.RewriteHash(SavedFacts[i]);

            DoesData.Clear();
            ReuserMgr.OnNewState();
        }

        /// <summary>
        /// Loads state from an arbitrary one.
        /// </summary>
        public void LoadState(RowSet[] fromState)
        {
            for (int i = 0; i < CurrentState.NextFacts.Length; ++i)
                NextFacts[i].DataNode.Data.RewriteHash(fromState[i]);

            DoesData.Clear();
            ReuserMgr.OnNewState();
        }
        public void LoadState(GDLState fromState)
        {
            for (int i = 0; i < CurrentState.NextFacts.Length; ++i)
                NextFacts[i].DataNode.Data.RewriteHash(fromState.NextFacts[i].DataNode.Data);

            DoesData.Clear();
            ReuserMgr.OnNewState();
        }

        public GDLState CurrentState = null;

        public FrameFact[] NextFacts
        {
            get { return CurrentState.NextFacts; }
            set { CurrentState.NextFacts = value; }
        }
        public RowSet DoesData
        {
            get { return CurrentState.DoesData; }
            set { CurrentState.DoesData = value; }
        }
        public SimulatorMoves[] LegalMoves = null;

     
        public RowSet LegalData = null;
        public NodeReuserManager ReuserMgr = null;

        public bool TerminalContainsLegal = false;
     
        /***** ---- RESULT ----- *****/
        public double[] Goals = null;
        short[] cppGoals = null;
        public int SIMULATOR_STATUTS = 0;
        public int Step = 0;
        /*****************************/

        public GDLSimulator(int interpreterID)
        {
            CurrentState = new GDLState();
            InterpreterID = interpreterID;
             
        }

        public void Init(int playersCount, NodeOR does, NodeOR legal, NodeOR[] goalByPlayer,NodeOR terminal, int frameFactsCount, NodeReuserManager reuserManager)
        {
            PlayersCount = playersCount;
            
            DoesNode = does;
            DoesData = DoesNode.Data;
           
            LegalNode = legal;
            LegalData = legal.Data;
            TerminalNode = terminal;
        
            GoalByPlayer = goalByPlayer;
            NextFacts = new FrameFact[frameFactsCount];
            SavedFacts = new RowSet[frameFactsCount];
            LegalMoves = new SimulatorMoves[playersCount];
            Goals = new double[playersCount];
            cppGoals = new short[playersCount];
            for (int i = 0; i < playersCount; ++i)
            {
                LegalMoves[i] = new SimulatorMoves(DoesNode.Data, LegalNode.Data);
            }
            ReuserMgr = reuserManager;
        }

        public SimulatorMoves[] CopyCurrentLegalMoves()
        {
            SimulatorMoves[] copy = new SimulatorMoves[PlayersCount];
            for (int i = 0; i < PlayersCount; ++i)
                copy[i] = LegalMoves[i].Clone();
            return copy;
        }

        public void Restart()
        {
            foreach (var next in NextFacts)
                next.DataNode.Data.RewriteHash(next.InitialData);

			DoesData.Clear();
        }

        public void PopulateLegalMoves()
        {
            int offset = 0;
            foreach (var entry in LegalMoves)
                entry.MoveStarts.Clear();

            for (int i = 0; i < LegalData.Count; ++i)
            {
                int player = LegalData.Data[offset];
                if (player < PlayersCount)
                    LegalMoves[player].MoveStarts.Add(offset);
                offset += LegalData.Arity;
            }
        }

        public void ComputeAvailableMoves()
        {
            LegalNode.Prove();
            PopulateLegalMoves();
            DoesData.Clear();
        }

        public void PerformNext()
        {
            DoesData.PerformHash();
            foreach (FrameFact fact in NextFacts)
            {
                if (fact.RuleNode != null && fact.RuleNode.Prove() == false)
                    fact.RuleNode.Data.Clear();
            }
            foreach (FrameFact fact in NextFacts)
            {
                if (fact.RuleNode != null)
                    fact.DataNode.Data.RewriteHash(fact.RuleNode.Data);
                else
                    fact.DataNode.Data.Clear();
            }
        }

		public void MonteCarlo()
		{
			SIMULATOR_STATUTS = 0;
			Step = 1;
			ReuserMgr.OnNewState();
			if (TerminalContainsLegal)
			{

				ComputeAvailableMoves();
			}
			while (Step < 1001)
			{
				if (!TerminalContainsLegal)
				{
					ReuserMgr.OnNewState();
					ComputeAvailableMoves();
				}

				for (int player = 0; player < PlayersCount; ++player)
					LegalMoves[player].ApplyRandom();


				PerformNext();
				ReuserMgr.OnNewState();
				++Step;
				if (TerminalContainsLegal)
				{
					ComputeAvailableMoves();
				}
				if (TerminalNode.Prove())
				{
					ComputeGoals();
					return;

				}
			}
			SIMULATOR_STATUTS = -100;
		}

		public TextState PlaySequence(List<TextMove> moves)
		{
			Step = 1;
			ReuserMgr.OnNewState();
			if (TerminalContainsLegal)
			{
				ComputeAvailableMoves();
			}

			int moveIndex = 0;
			try
			{
				foreach (TextMove move in moves)
				{
					TextState.BugLogger.MoveIndex = moveIndex;
					if (!TerminalContainsLegal)
					{
						ReuserMgr.OnNewState();
						ComputeAvailableMoves();
					}

					int[] appliedMoves = new int[PlayersCount];
					for (int pIndex = 0; pIndex < PlayersCount; ++pIndex)
					{
						appliedMoves[pIndex] = LegalMoves[pIndex].GetIndex(move.TokenizedMove[pIndex]);
						if (appliedMoves[pIndex] < 0)
						{
							if (TextState.IsPrediction == false)
							{
								throw new ArgumentException();
							}
							else
							{
								appliedMoves[pIndex] = 0;
							}
						}
					}

					for (int pIndex = 0; pIndex < PlayersCount; ++pIndex)
						LegalMoves[pIndex].Apply(appliedMoves[pIndex]);

					PerformNext();
					ReuserMgr.OnNewState();
					++Step;

					if (TerminalContainsLegal)
					{
						ComputeAvailableMoves();
					}

					++moveIndex;
					if (TerminalNode.Prove())
					{
						if (moveIndex < moves.Count)
						{
							TextState.BugLogger.Log(BugType.EARLY_TERMINAL, -1, -1, "", 0.0);
							return null;
						}
						return CurrentState.ToTextState("");
						
					}
				}
			}
			catch(Exception)
			{
				TextState.BugLogger.Log(BugType.INVALID_MOVE, -1, -1, $"A move in {moves[moveIndex].Text.Replace('\n', ' ')} is invalid.", 0.0);
				return null;
			}

			ReuserMgr.OnNewState();
			DoesData.Clear();
			PerformNext();
			return CurrentState.ToTextState("");
		}
		
		public TextMoveSequence GenerateSequence(int maxStep)
		{
			TextMoveSequence currentSequence = new TextMoveSequence();
			Step = 1;
			ReuserMgr.OnNewState();
			if (TerminalContainsLegal)
			{
				ComputeAvailableMoves();
			}
			while (Step < maxStep)
			{
				if (!TerminalContainsLegal)
				{
					ReuserMgr.OnNewState();
					ComputeAvailableMoves();
				}


				StringBuilder jsonMoveBuilder = new StringBuilder();
				for (int player = 0; player < PlayersCount; ++player)
				{
					int moveNumber = random.Next(LegalMoves[player].Count);
					var moveText = LegalMoves[player].MoveToText(moveNumber, true);
					jsonMoveBuilder.Append(moveText);
					if (player < PlayersCount - 1)
					{
						jsonMoveBuilder.Append("\\n");
					}

					LegalMoves[player].Apply(moveNumber);
				}

				currentSequence.Moves.Add(jsonMoveBuilder.ToString());

				PerformNext();
				ReuserMgr.OnNewState();
				++Step;

				if (TerminalContainsLegal)
				{
					ComputeAvailableMoves();
				}
				if (TerminalNode.Prove())
				{
					ComputeGoals();
					return currentSequence;

				}
			}
			return currentSequence;
		}

		public void GenerateStates(TextStateCollection dataset, int targetStateCount)
        {
            Random random = new Random();
            double currentProbability = -0.1;
            TextState currentTextState;

            int generatedInRun = 0;
            Step = 1;
            ReuserMgr.OnNewState();
            if (TerminalContainsLegal)
            {
                ComputeAvailableMoves();
            }
            while (Step < 1001)
            {
                if (!TerminalContainsLegal)
                {
                    ReuserMgr.OnNewState(); 
                    ComputeAvailableMoves();
                }

                StringBuilder jsonMoveBuilder = new StringBuilder();
				StringBuilder jsonAllMovesBuilder = new StringBuilder();
                for (int player = 0; player < PlayersCount; ++player)
                {
                    int moveNumber = random.Next(LegalMoves[player].Count);
                    var moveText = LegalMoves[player].MoveToText(moveNumber, true);

					jsonMoveBuilder.Append(moveText);
                    if (player < PlayersCount - 1)
                    {
                        jsonMoveBuilder.Append("\\n");
                    }
                    LegalMoves[player].Apply(moveNumber);
                }

                currentTextState = null;
                if (random.NextDouble() < currentProbability)
				{
					currentTextState = CurrentState.ToTextState(jsonMoveBuilder.ToString());
                    if(dataset.AddUniqueState(currentTextState))
                    {
                        ++generatedInRun;
						currentProbability = 0;
						for(int player = 0; player < PlayersCount;++ player)
						{
							for(int moveNumber = 0; moveNumber < LegalMoves[player].Count; ++moveNumber)
							{
								var moveText = LegalMoves[player].MoveToText(moveNumber, true);
								jsonAllMovesBuilder.Append(moveText);
								if (player < PlayersCount - 1 || moveNumber < LegalMoves[player].Count - 1)
								{
									jsonAllMovesBuilder.Append("\\n");
								}
							}
						}
						currentTextState.LegalMovesString = jsonAllMovesBuilder.ToString();

					}
                    else
                    {
                        currentTextState = null;
                    }
				}
				else
				{
					currentProbability += 0.075;
				}

				PerformNext();
                ReuserMgr.OnNewState();
                ++Step;
                
                if(currentTextState != null)
                {
                    dataset.AddAfterState(CurrentState.ToTextState(""));
					if (dataset.Size == targetStateCount || generatedInRun >= 4)
					{
						return;
					}
				}
                
                if (TerminalContainsLegal)
                {
                    ComputeAvailableMoves();
                }
                if (TerminalNode.Prove())
                {
                    ComputeGoals();
                    return;

                }
            }
        }

        int correctGoals = 0;
        public void ComputeGoals()
        {
            correctGoals = 0;
            for (int i = 0; i < PlayersCount; ++i)
            {
                foreach (NodeAND and in GoalByPlayer[i].Realizations)
                {
                    if (and.Prove())
                    {
                        ++correctGoals;
                        Goals[i] = (double)(0.01*ulong.Parse(Translator.Instance.ToSymbol(and.MainData.Data[1])));
                        break;
                    }
                }
            }
            if (correctGoals < PlayersCount)
                SIMULATOR_STATUTS = -10;
        }


        public void QuickAdvanceStep()
        {
            PerformNext();
            CurrentState.Depth++;
        }

        /// <summary>
        /// Returns FALSE if terminal
        /// </summary>
        public bool FullAdvanceStep()
        {
            ReuserMgr.OnNewState();
            SIMULATOR_STATUTS = 0;
            PerformNext();
            ReuserMgr.OnNewState();
            CurrentState.Depth++;
            if (TerminalContainsLegal)
            {
                ComputeAvailableMoves();
            }
            if (TerminalNode.Prove())
            {
                ComputeGoals();
                CurrentState.Terminal = true;
                return false;
            }
            if (!TerminalContainsLegal)
                ComputeAvailableMoves();
            return true;
        }

        public void MonteCarloCpp(int timeLeft)
        {
            if (CppErrorsCount < 100)
            {
                try
                {
                    SIMULATOR_STATUTS = CppConnector.Rollout(CurrentState, cppGoals, timeLeft, InterpreterID);
                    if (SIMULATOR_STATUTS == 0)
                    {
                        for (int player = 0; player < GDLSimulator.PlayersCount; ++player)
                            Goals[player] = (double)(0.01 * ulong.Parse(Translator.Instance.ToSymbol(cppGoals[player])));
                    }
                }
                catch (Exception)
                {
                    SIMULATOR_STATUTS = -100;
                }
                if (SIMULATOR_STATUTS < 0)
                {
                    ++CppErrorsCount;
                }
            }
            else
            {

                MonteCarlo();
            }
        }

        public RowSet[] CopyCurrentState()
        {
            RowSet[] stateFacts = new RowSet[CurrentState.NextFacts.Length];
            for (int i = 0; i < CurrentState.NextFacts.Length; ++i)
            {
                stateFacts[i] = new RowSet(CurrentState.NextFacts[i].DataNode.Data.Arity, CurrentState.NextFacts[i].DataNode.Data.Capacity);
                stateFacts[i].Rewrite(CurrentState.NextFacts[i].DataNode.Data);
            }
            return stateFacts;
        } 
    }
}