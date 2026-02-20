using Newtonsoft.Json;
using Parser;
using Parser.Simulator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace MainNode
{
	class MainNodeProgram
	{
		public static string gdlDirectory = @"..\..\..\..\..\No-Obfuscation\Games";		//TODO: enter your path to Games folder
		public static string baseResults = @"..\..\..\..\..\No-Obfuscation\Results";	//TODO: enter your path to the folder with Results

		/*********************************************************************************************/
		/*  //////////////////////////////////////  MAIN LOOP ////////////////////////////////////// */
		static void Main(string[] args)
		{
			Verifier verifier = new Verifier();

			//verifier.RunStaticVerification(ExperimentType.NEXT_STATE); return;
			//verifier.RunStaticVerification(ExperimentType.LEGAL); return;
			VerifyMultistep();
			//verifier.RunAdditionalGameComplexityAnalysis();
			//GenerateStateBenchmark(gdlDirectory);
			//GenerateMoveSequenceBenchmark(gdlDirectory");
		}

		public static void GenerateStateBenchmark(string topFolder)
		{
			var files = Directory.EnumerateFiles(gdlDirectory).ToArray();
			int index = File.Exists("index.txt") ? int.Parse(File.ReadAllText("index.txt")) : 0;

			Console.WriteLine("Index = " + index);
			Thread.Sleep(500);
			StreamWriter writer;
			bool restartApplication = false;
			if (index < files.Length - 1)
			{
				writer = new StreamWriter("index.txt", false);
				writer.WriteLine((index + 1).ToString());
				writer.Close();
				restartApplication = true;
			}
			else
			{
				File.Delete("index.txt");
			}

			string file = files[index];
			Console.Clear();
			Console.WriteLine($"Opening {file}");
			GDLParser parser = new GDLParser();
			var ruleSheet = parser.ParseFromFile(file, false);
			GDLCompiler compiler = new GDLCompiler(0, ruleSheet);
			var simulator = compiler.Compile();


			TextStateCollection dataset = new TextStateCollection();
			const int sampleCount = 60;
			while (dataset.Size < sampleCount)
			{
				simulator.Restart();
				try
				{
					simulator.GenerateStates(dataset, sampleCount);
					Console.WriteLine($"[{dataset.Size}]: success");
				}
				catch (Exception)
				{
					Console.WriteLine($"[{dataset.Size}]: error");
				}
			}

			string gameName = Path.GetFileNameWithoutExtension(file);
			string jsonFolder = Path.Combine(topFolder, $"_jsons");
			string separateFolder = Path.Combine(topFolder, $"{gameName}-single-files");


			dataset.SerializeToFile(jsonFolder, gameName, true);

			if (restartApplication)
				Application.Restart();
		}

		public static void GenerateMoveSequenceBenchmark(string topFolder)
		{
			var files = Directory.EnumerateFiles(gdlDirectory).ToArray();
			int index = File.Exists("index.txt") ? int.Parse(File.ReadAllText("index.txt")) : 0;

			Console.WriteLine("Index = " + index);
			Thread.Sleep(500);
			StreamWriter writer;
			bool restartApplication = false;
			if (index < files.Length - 1)
			{
				writer = new StreamWriter("index.txt", false);
				writer.WriteLine((index + 1).ToString());
				writer.Close();
				restartApplication = true;
			}
			else
			{
				File.Delete("index.txt");
			}

			string file = files[index];
			Console.Clear();
			Console.WriteLine($"Opening {file}");
			GDLParser parser = new GDLParser();
			var ruleSheet = parser.ParseFromFile(file, false);
			GDLCompiler compiler = new GDLCompiler(0, ruleSheet);
			var simulator = compiler.Compile();

			HashSet<TextMoveSequence> sequences = new HashSet<TextMoveSequence>();
			const int sampleCount = 100;
			while (sequences.Count < sampleCount)
			{
				simulator.Restart();
				try
				{
					sequences.Add(simulator.GenerateSequence(17));
					Console.WriteLine($"[{sequences.Count}]: success");
				}
				catch (Exception)
				{
					Console.WriteLine($"[{sequences.Count}]: error");
				}
			}

			string gameName = Path.GetFileNameWithoutExtension(file);
			string jsonFolder = Path.Combine(topFolder, $"_jsons");
			string separateFolder = Path.Combine(topFolder, $"{gameName}-single-files");


			TextMoveSequence.SerializeToFile(jsonFolder, gameName, sequences);
			TextMoveSequence.SerializeToFolder(separateFolder, sequences);

			if (restartApplication)
				Application.Restart();
		}
	
		public static void VerifyMultistep()
		{
			StreamWriter allResultsWriter = new StreamWriter(Path.Combine(baseResults, "all_results.tsv"), false);

			//TODO: enter your names of multistep folders (because not for all tasks we have all of them)
			List<string> multiStepFolders = new List<string>()
			{
					"Multistep State Generation n=1",
					"Multistep State Generation n=2",
					"Multistep State Generation n=3",
					"Multistep State Generation n=4",
					"Multistep State Generation n=5",
					"Multistep State Generation n=7",
					"Multistep State Generation n=10",
			};

			var files = Directory.EnumerateFiles(gdlDirectory).ToArray();
			int index = File.Exists("index.txt") ? int.Parse(File.ReadAllText("index.txt")) : 0;

			Console.WriteLine("Index = " + index);
			Thread.Sleep(100);
			StreamWriter writer;
			bool restartApplication = false;

			if (index < files.Length - 1)
			{
				writer = new StreamWriter("index.txt", false);
				writer.WriteLine((index + 1).ToString());
				writer.Close();
				restartApplication = true;
			}
			else
			{
				File.Delete("index.txt");
			}

			string file = files[index];
			string game = Path.GetFileNameWithoutExtension(file);
			string searchString = $"_{game}_";

			GDLParser parser = new GDLParser();
			var ruleSheet = parser.ParseFromFile(file, false);
			GDLCompiler compiler = new GDLCompiler(0, ruleSheet);
			var simulator = compiler.Compile();

			foreach (var resultDir in multiStepFolders)
			{
				TextState.IsPrediction = resultDir.Contains("_prediction");
				var modelDirs = Directory.EnumerateDirectories(Path.Combine(baseResults, resultDir));
				var specificDir = Path.Combine(baseResults, resultDir);
				MultistepResults resultObject = new MultistepResults(specificDir.Replace("_deobfuscated", "_obfuscated"));

				var errorFile = Path.Combine(specificDir, "details.txt").Replace("_deobfuscated", "_obfuscated");
				TextState.BugLogger.Open(errorFile, index > 0);

				TextState.BugLogger.Game = game;
				TextState.BugLogger.Experiment = resultDir.Replace("_deobfuscated", "_obfuscated");
				resultObject.OpenGame(game);
				foreach (var modelDir in modelDirs)
				{
					var matchingFiles = Directory.EnumerateFiles(modelDir)
						.Where(path => Path.GetFileName(path).Contains(searchString)).ToList();

					var model = Path.GetFileName(modelDir);
	
					TextState.BugLogger.Model = model;
					foreach (var matchingFile in matchingFiles)
					{
						Console.WriteLine($"Currently  processing {matchingFile}");
						string jsonText = File.ReadAllText(matchingFile);
						GameDataMultistep gameData = JsonConvert.DeserializeObject<GameDataMultistep>(jsonText);

						double totalDifference = 0;
						double correctCount = 0;
						int sampleIndex = 0;
						foreach (var sample in gameData.Samples)
						{
							TextState.BugLogger.SampleIndex = sampleIndex;
							++sampleIndex;
							List<TextMove> moves = new List<TextMove>();
							try
							{
								foreach (var move in sample.Moves)
								{
									moves.Add(new TextMove(move.JointMove));
								}
								simulator.Restart();
								TextState groundState = simulator.PlaySequence(moves);
								groundState.Facts["step"].Clear();
								groundState.Facts["step"].Add(moves.Count.ToString());
								TextState.BugLogger.MoveIndex = moves.Count;
								TextState llmState = new TextState(Verifier.StringToFacts(sample.LlmState));
								var diff = llmState.CalculateDifference(groundState);
								totalDifference += diff;
								if (diff >= 1 || diff > 0.9999999999)
									++correctCount;
							}
							catch(Exception)
							{
								
							}
						}

						resultObject.AddResults(model, totalDifference/ gameData.Samples.Count, correctCount/gameData.Samples.Count);
					}
				}

				TextState.BugLogger.Close();
				resultObject.SaveResults();

				allResultsWriter.WriteLine();
				allResultsWriter.WriteLine(resultDir);
				resultObject.AppendResults(allResultsWriter);
			}

			allResultsWriter.Close();
			if (restartApplication)
				Application.Restart();
		}
    }
}
