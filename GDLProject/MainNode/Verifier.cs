using Newtonsoft.Json;
using Parser.Simulator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MainNode
{
	public class StateSample
	{
		[JsonProperty("game_state")]
		public string GameState { get; set; }

		[JsonProperty("move")]
		public string Move { get; set; }

		[JsonProperty("next_state")]
		public string NextState { get; set; }

		[JsonProperty("llm_state")]
		public string LlmState { get; set; }

		[JsonProperty("llm_is_terminal")]
		public int LlmIsTerminal { get; set; }
	}

	public class LegalMovesSample
	{
		[JsonProperty("game_state")]
		public string GameState { get; set; }

		[JsonProperty("legal_moves")]
		public string NextState { get; set; }

		[JsonProperty("llm_legal_moves")]
		public string LlmState { get; set; }
	}

	public class MultistepMoveSample
	{
		[JsonProperty("step")]
		public string Step { get; set; }

		[JsonProperty("joint_move")]
		public string JointMove { get; set; }
	}
	public class MultistepSample
	{
		[JsonProperty("moves")]
		public List<MultistepMoveSample> Moves { get; set; }

		[JsonProperty("llm_state")]
		public string LlmState { get; set; }
	}

	public class GameDataStates
	{
		[JsonProperty("game_name")]
		public string GameName { get; set; }

		[JsonProperty("llm_model")]
		public string LlmModel { get; set; }

		[JsonProperty("experiment_type")]
		public string ExperimentType { get; set; }
		
		[JsonProperty("samples")]
		public List<StateSample> Samples { get; set; }
	}

	public class GameDataLegal
	{
		[JsonProperty("game_name")]
		public string GameName { get; set; }

		[JsonProperty("llm_model")]
		public string LlmModel { get; set; }

		[JsonProperty("experiment_type")]
		public string ExperimentType { get; set; }

		[JsonProperty("samples")]
		public List<LegalMovesSample> Samples { get; set; }
	}

	public class GameDataMultistep
	{
		[JsonProperty("game_name")]
		public string GameName { get; set; }

		[JsonProperty("llm_model")]
		public string LlmModel { get; set; }

		[JsonProperty("samples")]
		public List<MultistepSample> Samples { get; set; }
	}

	public enum ExperimentType
	{
		NEXT_STATE,
		LEGAL
	}

	public class Verifier
	{
		MultistepResults resultObject;
		const string nextStateBasePath = @"C:\Users\Maciek\Documents\GitHub\GGP_LLM_Experiment\Results\next_state_obfuscated";
		const string legalMovesBasePath = @"C:\Users\Maciek\Documents\GitHub\GGP_LLM_Experiment\Results\legal_moves";
		const string multistepBasePath = @"C:\Users\Maciek\Documents\GitHub\GGP_LLM_Experiment\Results\multi_step_generation_n5";

		public static List<string> StringToFacts(string stateText)
		{
			stateText = stateText.Replace("\n ", "\n");
			stateText = stateText.Replace("\n\n", "\n");
			stateText = stateText.Replace("]", ")");
			stateText = stateText.Replace("[", "(");
			stateText = stateText.Replace(")(", ")\n(");
			stateText = stateText.Replace(")   (", ")\n(");
			stateText = stateText.Replace(") (", ")\n(");
			var list = stateText.Split('\n').ToList();

			return list;
		}

		public void RunStaticVerification(ExperimentType experimentType)
		{
			string basePath = "";
			switch (experimentType)
			{
				case ExperimentType.NEXT_STATE:
					basePath = nextStateBasePath;
					break;
				case ExperimentType.LEGAL:
					basePath = legalMovesBasePath;
					break;
				default:
					break;
			}

			if (Directory.Exists(basePath) == false)
			{
				Console.WriteLine($"Folder {basePath} does not exist.");
				return;
			}

			StreamWriter allResultsWriter = new StreamWriter(Path.Combine(basePath, "results_all.tsv"), false);
			resultObject = new MultistepResults(basePath);
			var directories = Directory.EnumerateDirectories(basePath);
			var errorFile = Path.Combine(basePath, "results_details.txt");
			TextState.BugLogger.Open(errorFile, false);

			foreach (var directory in directories)
			{
				switch (experimentType)
				{
					case ExperimentType.NEXT_STATE:
						RunAfterStateVerificationForModel(directory);
						break;
					case ExperimentType.LEGAL:
						RunLegalMovesVerificationForModel(directory);
						break;
					default:
						break;
				}	
			}

			TextState.BugLogger.Close();
			resultObject.SaveResults();
			resultObject.AppendResults(allResultsWriter);
			allResultsWriter.Close();
		}

		public void RunAdditionalGameComplexityAnalysis()
		{
			string directory = Path.Combine(nextStateBasePath, @"gemini-2.5-pro");
			Console.WriteLine($"Game complexity: reading folder: {directory}");
			var files = Directory.EnumerateFiles(directory).ToList();

			List<string> gameNames = new List<string>();
			Dictionary<string, (double, double)> complexityValues = new Dictionary<string, (double, double)>();
			foreach (var file in files)
			{
				if (Path.GetFileName(file).StartsWith("results"))
				{
					continue;
				}

				Console.Write($"Reading file: {file}");
				string jsonText = File.ReadAllText(file);
				GameDataStates gameData = JsonConvert.DeserializeObject<GameDataStates>(jsonText);
				Console.WriteLine($" Sample count: {gameData.Samples.Count}");
				double totalDifference = 0;
				double totalCount = 0;
				foreach (var sample in gameData.Samples)
				{
					TextState state = new TextState(StringToFacts(sample.GameState));
					TextState nextStateGround = new TextState(StringToFacts(sample.NextState));
					var diff = (double)nextStateGround.CalculateDifferenceIntegerFacts(state) / 2;
					totalDifference += diff;
					totalCount += (state.TotalFactCount + nextStateGround.TotalFactCount);
				}

				totalDifference /= gameData.Samples.Count;
				totalCount /= (double)(2.0 * gameData.Samples.Count);
				gameNames.Add(gameData.GameName);
				complexityValues[gameData.GameName] = (totalCount, totalDifference);
			}

			gameNames.Sort();

			StreamWriter writer = new StreamWriter(Path.Combine(directory, "results_complexity.txt"));
			foreach (var gameName in gameNames)
			{
				writer.WriteLine($"{gameName}\t{complexityValues[gameName].Item1}\t{complexityValues[gameName].Item2}");
			}
			writer.Close();
		}
		
		public void RunAfterStateVerificationForModel(string directory)
		{
			Console.WriteLine($"State verification: reading folder: {directory}");
			var files = Directory.EnumerateFiles(directory).ToList();

			Dictionary<string, string> accuracyValues = new Dictionary<string, string>();
			Dictionary<string, string> correctValues = new Dictionary<string, string>();
			List<string> gameNames = new List<string>();
			foreach (var file in files)
			{
				if(Path.GetFileName(file).StartsWith("results") || Path.GetFileName(file).StartsWith("details") || Path.GetFileName(file).StartsWith("."))
				{
					continue;
				}

				Console.Write($"Reading file: {file}");
				string jsonText = File.ReadAllText(file);
				GameDataStates gameData = JsonConvert.DeserializeObject<GameDataStates>(jsonText);
				TextState.BugLogger.Game = gameData.GameName;
				TextState.BugLogger.Model= gameData.LlmModel;
				TextState.BugLogger.Experiment = gameData.ExperimentType;
				resultObject.OpenGame(gameData.GameName);

				Console.WriteLine($" Sample count: {gameData.Samples.Count}");
				double totalDifference = 0;
				int correctCount = 0;
				int sampleIndex = 0;
				foreach(var sample in gameData.Samples)
				{
					TextState.BugLogger.SampleIndex = sampleIndex;
					++sampleIndex;
					TextState nextState = new TextState(StringToFacts(sample.LlmState));
					TextState nextStateGround = new TextState(StringToFacts(sample.NextState));

					var diff = nextState.CalculateDifference(nextStateGround);
					totalDifference += diff;
					if(diff >= 1 || diff > 0.9999999999)
					{
						++correctCount;
					}
				}

				double jaccardIndex = gameData.Samples.Count > 0 ? (totalDifference / gameData.Samples.Count) : -1;
				double correctRatio = gameData.Samples.Count > 0 ? ((double)correctCount / gameData.Samples.Count) : -1;
				accuracyValues[gameData.GameName] = jaccardIndex > -0.5 ? jaccardIndex.ToString() : "NoData";
				correctValues[gameData.GameName] = correctRatio > -0.5 ?  correctRatio.ToString() : "NoData";
				gameNames.Add(gameData.GameName);
				resultObject.AddResults(gameData.LlmModel, jaccardIndex, correctRatio);
			}
		
			gameNames.Sort();

			StreamWriter writer = new StreamWriter(Path.Combine(directory, $"results.txt"));
			foreach (var gameName in gameNames)
			{
				var logText = $"{gameName}\t{accuracyValues[gameName]}\t{correctValues[gameName]}";
				writer.WriteLine(logText);
			}
			writer.Close();
		}

		public void RunLegalMovesVerificationForModel(string directory)
		{
			Console.WriteLine($"State verification: reading folder: {directory}");
			var files = Directory.EnumerateFiles(directory).ToList();

			Dictionary<string, string> accuracyValues = new Dictionary<string, string>();
			Dictionary<string, string> correctValues = new Dictionary<string, string>();
			List<string> gameNames = new List<string>();
			foreach (var file in files)
			{
				if (Path.GetFileName(file).StartsWith("results") || Path.GetFileName(file).StartsWith("details") || Path.GetFileName(file).StartsWith("."))
				{
					continue;
				}

				Console.Write($"Reading file: {file}");

				string jsonText = File.ReadAllText(file);
				GameDataLegal gameData = JsonConvert.DeserializeObject<GameDataLegal>(jsonText);
				TextState.BugLogger.Game = gameData.GameName;
				TextState.BugLogger.Model = gameData.LlmModel;
				TextState.BugLogger.Experiment = gameData.ExperimentType;
				resultObject.OpenGame(gameData.GameName);

				Console.WriteLine($" Sample count: {gameData.Samples.Count}");
				double totalDifference = 0;
				int correctCount = 0;
				int sampleIndex = 0;
				foreach (var sample in gameData.Samples)
				{
					TextState.BugLogger.SampleIndex = sampleIndex;
					++sampleIndex;

					TextState nextState = new TextState(StringToFacts(sample.LlmState));
					TextState nextStateGround = new TextState(StringToFacts(sample.NextState));

					var diff = nextState.CalculateDifference(nextStateGround);
					totalDifference += diff;
					if (diff >= 1 || diff > 0.9999999999)
					{
						++correctCount;
					}
				}

				double jaccardIndex = gameData.Samples.Count > 0 ? (totalDifference / gameData.Samples.Count) : -1;
				double correctRatio = gameData.Samples.Count > 0 ? ((double)correctCount / gameData.Samples.Count) : -1;
				accuracyValues[gameData.GameName] = jaccardIndex > -0.5 ? jaccardIndex.ToString() : "NoData";
				correctValues[gameData.GameName] = correctRatio > -0.5 ? correctRatio.ToString() : "NoData";
				gameNames.Add(gameData.GameName);
				resultObject.AddResults(gameData.LlmModel, jaccardIndex, correctRatio);
			}

			gameNames.Sort();

			StreamWriter writer = new StreamWriter(Path.Combine(directory, $"results.txt"));
			foreach (var gameName in gameNames)
			{
				var logText = $"{gameName}\t{accuracyValues[gameName]}\t{correctValues[gameName]}";
				writer.WriteLine(logText);
			}
			writer.Close();
		}
	}
}
