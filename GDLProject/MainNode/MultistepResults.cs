using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MainNode
{
	internal class MultistepResults
	{
		public readonly string Directory;

		//All model names which have been observed.
		public List<string> Models = new List<string>();

		//The structure is: [GameName, [ModelName, Results]]
		public Dictionary<string, Dictionary<string, string>> Results = new Dictionary<string, Dictionary<string, string>>();

		//Helper because long computations are done per given game until the algorithm goes to another
		private Dictionary<string, string> currentResults = null; //currently used value of Results; Results[Game]

		public MultistepResults(string path)
		{
			Directory = path;
			LoadResults();
		}

		public void OpenGame(string gameName)
		{
			if (Results.TryGetValue(gameName, out currentResults) == false)
			{
				currentResults = new Dictionary<string, string>();
				Results.Add(gameName, currentResults);
			}

		}

		public void AddResults(string model, double result1, double result2)
		{
			if (Models.Contains(model) == false)
			{
				Models.Add(model);
			}

			if (currentResults.ContainsKey(model) == false)
			{
				currentResults.Add(model, $"{result1}\t{result2}"); //we combine two results into one string
			}

			currentResults[model] = $"{result1}\t{result2}";
		}
		public void LoadResults()
		{
			string file = Path.Combine(Directory, "results.txt");
			if (File.Exists(file) == false)
				return;

			Results.Clear();
			Models.Clear();
			currentResults = new Dictionary<string, string>();

			var lines = File.ReadAllLines(file);
			if (lines.Length == 0)
				return;

			// --- HEADER ---
			// GameName \t Model1 \t Model2 \t ...
			var headerParts = lines[0].Split('\t');

			if (headerParts.Length < 2 || headerParts[0] != "GameName")
				throw new FormatException("Invalid results file header.");

			for (int i = 1; i < headerParts.Length; i++)
			{
				Models.Add(headerParts[i]);
			}

			// --- DATA ROWS ---
			for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
			{
				var line = lines[lineIndex];
				if (string.IsNullOrWhiteSpace(line))
					continue;

				var parts = line.Split('\t');

				// Expected: 1 + 2 * Models.Count columns
				int expectedColumns = 1 + 2 * Models.Count;
				if (parts.Length != expectedColumns)
					throw new FormatException(
						$"Invalid column count in line {lineIndex + 1}. Expected {expectedColumns}, got {parts.Length}.");

				string gameName = parts[0];
				var gameResults = new Dictionary<string, string>();

				int p = 1;
				for (int m = 0; m < Models.Count; m++)
				{
					string v1 = parts[p++];
					string v2 = parts[p++];

					if (v1 == "-" && v2 == "-")
						continue;

					gameResults.Add(Models[m], $"{v1}\t{v2}");
				}

				Results.Add(gameName, gameResults);
			}
		}

		public void AppendResults(StreamWriter writer)
		{
			if (Models.Count == 0)
			{
				return;
			}

			var sb = new StringBuilder();

			sb.Append("GameName");
			foreach (var model in Models)
			{
				sb.Append('\t');
				sb.Append(model);
			}
			sb.AppendLine();

			var orderedGames = Results.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

			foreach (var game in orderedGames)
			{
				var gameResults = Results[game];
				if (gameResults.Count == 0)
					continue;

				sb.Append(game);
				foreach (var model in Models)
				{
					sb.Append('\t');
					if (gameResults.TryGetValue(model, out var value))
					{
						sb.Append(value);      // already "result1\tresult2"
					}
					else
					{
						sb.Append("-1 \t -1");
					}
				}

				sb.AppendLine();
			}

			writer.Write(sb.ToString());
		}

		public void SaveResults()
		{
			if (Models.Count == 0)
			{
				return;
			}

			string file = Path.Combine(Directory, "results.txt");
			if (!string.IsNullOrEmpty(Directory))
			{
				System.IO.Directory.CreateDirectory(Directory);
			}

			var sb = new StringBuilder();

			sb.Append("GameName");
			foreach (var model in Models)
			{
				sb.Append('\t');
				sb.Append(model);
			}
			sb.AppendLine();

			var orderedGames = Results.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

			foreach (var game in orderedGames)
			{
				var gameResults = Results[game];
				if (gameResults.Count == 0)
					continue;

				sb.Append(game);

				foreach (var model in Models)
				{
					sb.Append('\t');
					if (gameResults.TryGetValue(model, out var value))
					{
						sb.Append(value);      // already "result1\tresult2"
					}
					else
					{
						sb.Append("-1 \t -1");
					}
				}

				sb.AppendLine();
			}

			System.IO.File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
		}
	}
}
