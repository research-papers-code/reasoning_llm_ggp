using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Parser.Serialization;
using System.IO;

namespace Parser.Simulator
{
    public class TextState : IEquatable<TextState>
	{
		public static BugLogger BugLogger = new BugLogger();
		public static bool IsPrediction = false;

		public Dictionary<string, HashSet<string>> Facts { get; private set; } = new Dictionary<string, HashSet<string>>();
		public Dictionary<string, HashSet<string>> LowFacts { get; private set; } = new Dictionary<string, HashSet<string>>();

		public string MoveString { get; private set; }

		public string LegalMovesString { get; set; }
            
        public int ID { get; set; }

        public int TotalFactCount => Facts.Sum(x => x.Value.Count);

		public TextState(string moveString)
		{
			this.MoveString = moveString;
		}

        public TextState(List<string> texts)
        {
            foreach(var input in texts)
            {
                var text = input.Replace("(", "").Replace(")", "");
                string name, remainder;
                int spaceIndex = text.IndexOf(' ');
                if(spaceIndex  == -1)
                {
                    name = text;
                    remainder = "";
                }
                else
                {
					name = text.Substring(0, spaceIndex);
					remainder = text.Substring(spaceIndex + 1);
				}

                AddFact(name, remainder);
			}
        }


		public int CalculateDifferenceIntegerFacts(TextState other)
		{
			int commonCount = 0;
			int countOurs = TotalFactCount;
			int countTheis = other.TotalFactCount;
			foreach (var entry in Facts)
			{
				if (other.Facts.TryGetValue(entry.Key, out var otherFacts))
				{
					foreach (var fact in entry.Value)
					{
						if (otherFacts.Contains(fact))
						{
							++commonCount;
						}
					}
				}
			}

			return (countOurs + countTheis - 2*commonCount);
		}

		public double CalculateDifference(TextState groundState)
        {
            int commonCount = 0;
            int countOurs = TotalFactCount;
            int countTheirs = groundState.TotalFactCount;

			if (countOurs == 0 && countTheirs == 0)
				return 1.0;

			
			string extraMessage = "";
			if (countOurs > countTheirs)
			{
				
				foreach(var entry in Facts)
				{
					if(groundState.Facts.TryGetValue(entry.Key, out var otherFacts))
					{
						foreach(var fact in entry.Value)
						{
							if(otherFacts.Contains(fact)==false)
							{
								extraMessage = $"[Extra fact example: ({entry.Key} {fact})]";
							}
						}
					}
					else
					{
						extraMessage = $"[Extra fact group example: {entry.Key}]";
					}
				}
			}

			int missingCount = 0;
			string missingMessage = "";
			foreach (var entry in groundState.Facts)
            {
                if (Facts.TryGetValue(entry.Key, out var facts))
                {
                    foreach(var fact in entry.Value)
                    {
                        if(facts.Contains(fact) || (LowFacts.ContainsKey(entry.Key.ToLower()) && LowFacts[entry.Key.ToLower()].Contains(fact.ToLower())))
                        {
                            ++commonCount;
                        }
						else
						{
							++missingCount;
							missingMessage  = $"[Missing fact example: ({entry.Key} {fact})]";
	

						}
                    }
                }
				else
				{
					missingCount += entry.Value.Count;
					missingMessage = $"[Missing fact group example: {entry.Key}]";
				}
            }

			
            double index = commonCount / (double)(countOurs + countTheirs - commonCount);

			BugLogger.Log(BugType.NATURAL, Math.Max(0,countOurs - countTheirs), missingCount, extraMessage+"\t"+missingMessage, index);
            return index;
        }

		public void AddFact(string name, string fullText)
        {
			if (string.IsNullOrWhiteSpace(name))
				return;

			if (string.IsNullOrWhiteSpace(fullText))
				fullText = string.Empty;

			if (name.Equals("true") && fullText.Length > 0)
			{
				int idx = fullText.IndexOf(' ');
				string newName = idx >= 0 ? fullText.Substring(0, idx) : fullText;
				string newFullText = idx >= 0 ? fullText.Substring(idx + 1) : string.Empty;
				AddFact(newName, newFullText);
				return;
			}

			if (!Facts.TryGetValue(name, out var set))
			{
				set = new HashSet<string>();
				Facts[name] = set;
				LowFacts.Add(name.ToLower(), new HashSet<string>());
			}

			set.Add(fullText);
			LowFacts[name.ToLower()].Add(fullText.ToLower());
		}

        public string StateToJsonString()
        {
			StringBuilder sb = new StringBuilder();

            sb.Append("\"");
			foreach (var entry in Facts)
			{
				foreach (var fact in entry.Value)
				{
					sb.Append($"({entry.Key} {fact})\\n");
				}
			}
            sb.Remove(sb.Length - 2, 2);
			sb.Append("\"");
			return sb.ToString();
		}
		
		public string StateToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(var entry in Facts)
            {
                foreach(var fact in entry.Value)
                {
                    sb.AppendLine($"({entry.Key} {fact})");
                }
            }

            return sb.ToString();
        }

		public bool Equals(TextState other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals(this, other))
				return true;

			if (Facts.Count != other.Facts.Count)
				return false;

            if (!MoveString.Equals(other.MoveString))
                return false;

			foreach (var kvp in Facts)
			{
				if (!other.Facts.TryGetValue(kvp.Key, out var otherSet))
					return false;

				if (!kvp.Value.SetEquals(otherSet))
					return false;
			}

			return true;
		}

		public override bool Equals(object obj) => Equals(obj as TextState);

		public override int GetHashCode()
		{
			int hash = 17;
			foreach (var kvp in Facts.OrderBy(k => k.Key))
			{
				hash = hash * 31 + kvp.Key.GetHashCode();
				foreach (var fact in kvp.Value.OrderBy(f => f))
					hash = hash * 31 + fact.GetHashCode();
			}
			return hash;
		}
	}

	public class TextMove
	{
		public string Text { get; private set; }
		public short[][] TokenizedMove { get; private set; }

        public TextMove(string text)
        {
			if(text.Contains("legal"))
			{
				text = text.Replace("legal","").Trim();
			}

			if(text.Contains("does"))
			{
				text = text.Replace("does", "").Trim();
			}

			text = text.Replace("), ", ") ");

			this.Text = text;
			TokenizedMove = ExpressionToken.TokenizeJointMove2(Text);
		}

		public override string ToString()
		{
			return $"{Text} {string.Join(" ", TokenizedMove[0])}";
		}
	}

	public class TextMoveSequence : IEquatable<TextMoveSequence>
	{
		public List<string> Moves { get; private set; } = new List<string>();

		public override bool Equals(object obj) =>
			Equals(obj as TextMoveSequence);

		public bool Equals(TextMoveSequence other)
		{
			if (other == null)
				return false;

			if (Moves.Count != other.Moves.Count)
				return false;

			for (int i = 0; i < Moves.Count; i++)
			{
				if (!string.Equals(Moves[i], other.Moves[i], StringComparison.Ordinal))
					return false;
			}

			return true;
		}

		public override int GetHashCode()
		{
			unchecked // allow arithmetic overflow
			{
				int hash = 17;
				foreach (var move in Moves)
				{
					hash = hash * 31 + (move?.GetHashCode() ?? 0);
				}
				return hash;
			}
		}

		public static void SerializeToFile(string jsonFolder, string gameName, HashSet<TextMoveSequence> sequence)
		{
			if (Directory.Exists(jsonFolder) == false)
				Directory.CreateDirectory(jsonFolder);

			var filename = Path.Combine(jsonFolder, gameName + ".json");
			StreamWriter writer = new StreamWriter(filename, false);

			writer.WriteLine("{");
			writer.WriteLine($"\t\"game_name\": \"{gameName}\",");
			writer.WriteLine($"\t\"samples\": [");

			int sampleIndex = 0;
			foreach (var sample in sequence)
			{
				writer.WriteLine("\t\t{");
				writer.WriteLine("\t\t\t\"moves\": [");

				for (int i = 0; i < sample.Moves.Count; i++)
				{
					writer.WriteLine("\t\t\t{");
					writer.WriteLine($"\t\t\t\t\"step\": \"{i}\",");
					writer.WriteLine($"\t\t\t\t\"joint_move\": \"{sample.Moves[i]}\"");
					writer.Write("\t\t\t}");

					if (i < sample.Moves.Count - 1)
						writer.WriteLine(",");
					else
						writer.WriteLine();
				}

				writer.WriteLine("\t\t\t]");  // close moves array
				writer.Write("\t\t}");        // close sample object

				if (sampleIndex < sequence.Count - 1)
					writer.WriteLine(",");
				else
					writer.WriteLine();

				sampleIndex++;
			}

			writer.WriteLine("\t]");
			writer.WriteLine("}");
			writer.Close();
		}

		public static void SerializeToFolder(string foldername, HashSet<TextMoveSequence> sequence)
		{
			if (!Directory.Exists(foldername))
				Directory.CreateDirectory(foldername);

			int index = 0;
			foreach (var sample in sequence)
			{
				StreamWriter writerStates = new StreamWriter(Path.Combine(foldername, $"sequence{index}.txt"));
				foreach (var move in sample.Moves)
				{
					writerStates.WriteLine(move);
				}
				
				writerStates.Close();
				++index;
			}
		}
	}

	public class TextStateCollection
	{
		HashSet<TextState> states = new HashSet<TextState>();
        List<TextState> afterStates = new List<TextState>();

		public bool AddUniqueState(TextState state)
		{
			if (states.Contains(state))
			{
				return false;
			}

            state.ID = states.Count;
            states.Add(state);
			return true;

		}

        public void AddAfterState(TextState state)
        {
            afterStates.Add(state);
        }

        public int Size => states.Count;


        public void SerializeToFile(string jsonFolder, string gameName, bool includeAfterStates)
        {
            if (Directory.Exists(jsonFolder) == false)
                Directory.CreateDirectory(jsonFolder);

            var filename = Path.Combine(jsonFolder, gameName + ".json");
            StreamWriter writer = new StreamWriter(filename, false);

            writer.WriteLine("{");
            writer.WriteLine($"\t\"game_name\": \"{gameName}\",");
            writer.WriteLine($"\t\"samples\": [");

            int index = 0;
            foreach (var sample in states)
            {
				writer.WriteLine("\t\t{");

                
				writer.WriteLine($"\t\t\t\"game_state\": {sample.StateToJsonString()},");
				writer.WriteLine($"\t\t\t\"move\": \"{sample.MoveString}\",");
				writer.WriteLine($"\t\t\t\"legal_moves\": \"{sample.LegalMovesString}\",");
				writer.WriteLine($"\t\t\t\"next_state\": {afterStates[sample.ID].StateToJsonString()}");

				++index;
                if(index >= states.Count)
                {
                    writer.WriteLine("\t\t}");
                }
                else
                {
					writer.WriteLine("\t\t},");
				}
            }
            writer.WriteLine($"\t]");
            writer.WriteLine("}");
            writer.Close();
        }

        public void SerializeToFolder(string foldername)
        {
            if(!Directory.Exists(foldername))
                Directory.CreateDirectory(foldername);

            int index = 0;
            foreach (var sample in states)
            {
                StreamWriter writerStates = new StreamWriter(Path.Combine(foldername,$"state{index}.txt"));
                StreamWriter writerMoves = new StreamWriter(Path.Combine(foldername, $"move{index}.txt"));
				StreamWriter writerAllMoves = new StreamWriter(Path.Combine(foldername, $"legal_moves{index}.txt"));
                StreamWriter writerAfterStates = new StreamWriter(Path.Combine(foldername, $"after_state{index}.txt"));

                writerStates.Write(sample.StateToString());

                var moveText = sample.MoveString.Replace("\\n", " ");
                writerMoves.WriteLine(moveText);
				writerAfterStates.Write(afterStates[sample.ID].StateToString());
				writerAllMoves.WriteLine(sample.LegalMovesString);
                writerStates.Close();
                writerMoves.Close();
                writerAfterStates.Close();
				writerAllMoves.Close();
                ++index;
            }
        }
	}

	public class GDLState
    {
        const int intSize = sizeof(int);

        public FrameFact[] NextFacts = null;
        public RowSet DoesData = null;
        public bool Terminal = false;
        public uint Depth = 0;

        public TextState ToTextState(string moveString)
        {
            TextState state = new TextState(moveString);
            foreach (FrameFact ff in NextFacts)
            {
                var rowset = ff.DataNode.Data;
                for (int rowIndex = 0; rowIndex < rowset.Count; ++rowIndex)
                {
                    var text = rowset.RowToString(rowIndex);
                    state.AddFact(ff.DataNode.Name, text);
                }
            }

            StringBuilder doesText = new StringBuilder();
            for(int rowIndex = 0; rowIndex < DoesData.Count;++rowIndex)
            {
                doesText.AppendLine(DoesData.RowToString(rowIndex));
            }

			return state;
        }

        public void Serialize(StateSerializer stateSerializer)
        {
            int size = 0;
            foreach (FrameFact ff in NextFacts)
            {
                size += ff.DataNode.Data.Count*ff.DataNode.Data.Stride;
                size += intSize;
            }
            if (stateSerializer.Buffer.Length < size)
                stateSerializer.Buffer = new byte[size];

            stateSerializer.Count = 0;
            foreach (FrameFact ff in NextFacts)
            {
                byte[] countBytes = BitConverter.GetBytes(ff.DataNode.Data.Count);
                for (int i = 0; i < intSize; ++i)
                    stateSerializer.Buffer[stateSerializer.Count++] = countBytes[i];
                Buffer.BlockCopy(ff.DataNode.Data.Data, 0, stateSerializer.Buffer, stateSerializer.Count, ff.DataNode.Data.Count * ff.DataNode.Data.Stride);
                stateSerializer.Count += (ff.DataNode.Data.Count*ff.DataNode.Data.Stride);
            }
        }

        public void Deserialize(StateSerializer stateSerializer)
        {
            int offset = 0;
            int count;
            foreach (FrameFact ff in NextFacts)
            {
                count = BitConverter.ToInt32(stateSerializer.Buffer, offset);
                offset += intSize;
                ff.DataNode.Data.Reallocate(count);
                ff.DataNode.Data.Count = count;
                Buffer.BlockCopy(stateSerializer.Buffer, offset, ff.DataNode.Data.Data, 0, count * ff.DataNode.Data.Stride);
                offset += count * ff.DataNode.Data.Stride;
                ff.DataNode.Data.PerformHash();
            }
        }


        public static void Serialize(StateSerializer stateSerializer, RowSet[] state)
        {
            int size = 0;
            foreach (RowSet data in state)
            {
                size += data.Count * data.Stride;
                size += intSize;
            }
            if (stateSerializer.Buffer.Length < size)
                stateSerializer.Buffer = new byte[size];

            stateSerializer.Count = 0;
            foreach (RowSet data in state)
            {
                byte[] countBytes = BitConverter.GetBytes(data.Count);
                for (int i = 0; i < intSize; ++i)
                    stateSerializer.Buffer[stateSerializer.Count++] = countBytes[i];
                Buffer.BlockCopy(data.Data, 0, stateSerializer.Buffer, stateSerializer.Count, data.Count * data.Stride);
                stateSerializer.Count += (data.Count * data.Stride);
            }
        }
    }

	public enum BugType
	{
		INVALID_MOVE,
		INVALID_SYMBOL,
		EARLY_TERMINAL,
		NATURAL
	}

	public class BugLogger
	{
		public string Game = "";
		public string Model = "";
		public string Experiment ="";
		public int SampleIndex;
		public int MoveIndex;

		private StreamWriter writer;

		public void Log(BugType bugType, int extraFacts, int misssingFacts, string message, double score)
		{
			writer.WriteLine($"{Game}\t{Experiment}\t{Model}\t{SampleIndex}\t{MoveIndex}\t{bugType.ToString()}\t{score.ToString("0.000")}\t{extraFacts}\t{misssingFacts}\t{message}");
		}

		public void Open(string file, bool append)
		{
			writer = new StreamWriter(file, append);
		}

		public void Close()
		{
			writer.Close();
		}
	}
}
