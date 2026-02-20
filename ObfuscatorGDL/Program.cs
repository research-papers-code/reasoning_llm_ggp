using ObfuscatorGGP;
using System.Text;

internal class Program
{
	private static string baseFolder = @"..\..\..\..\..\Obfuscation-1\Results";
	private static HashSet<char> separators = new HashSet<char>() { '\t', '\n', ' ', '(', ')', '{', '}', '[', ']', '"', ',', ':', '<', '='};
	private static NameChanger changer = null;
	private static readonly Random _rng = new Random();

	private static void Main(string[] args)
	{
		Console.WriteLine("Uncomment chosen functions and set paths properly");

		//Obfuscate();
		//DeObfuscate();
		//ChangeEncoding();
	}

	private static string GenerateUniqueRandomName(Dictionary<string, string> map, string key)
	{
		const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
		const string lettersAndDigits = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

		string name;

		do
		{
			int length = _rng.Next(5, 9); // 5–8 inclusive

			char firstChar = letters[_rng.Next(letters.Length)];

			char[] chars = new char[length];
			chars[0] = firstChar;

			for (int i = 1; i < length; i++)
				chars[i] = lettersAndDigits[_rng.Next(lettersAndDigits.Length)];

			name = new string(chars);

		} while (map.ContainsValue(name));

		map[key] = name;
		return name;
	}

	private static void ChangeEncoding(string inputFile, string outputFile)
	{
		var lines = File.ReadAllLines(inputFile);

		StreamWriter writer = new StreamWriter(outputFile);
		Dictionary<string, string> mapping = new Dictionary<string, string>();
		foreach(var line in lines)
		{
			var parts = line.Split(' ');
			if (parts[1].StartsWith("?term"))
			{
				string nameWithout = parts[1].Substring(1);

				if (!mapping.ContainsKey(nameWithout))
					GenerateUniqueRandomName(mapping, nameWithout);
				
				writer.WriteLine($"{parts[0]} ?{mapping[nameWithout]}");
			}
			else if (parts[1].StartsWith("term") && parts[1].Equals("terminal")==false)
			{
				if (!mapping.ContainsKey(parts[1]))
					GenerateUniqueRandomName(mapping, parts[1]);

				writer.WriteLine($"{parts[0]} {mapping[parts[1]]}");
			}
			else
			{
				writer.WriteLine(line);
			}
		}
		writer.Close();
	}

	private static void Compare()
	{
		var files = Directory.EnumerateFiles(Path.Combine(baseFolder, @"Games\Games"));
		foreach(var file in files)
		{
			var filename = Path.GetFileName(file);
			Console.WriteLine("Comparing " + filename);
			Compare(@"Games\Games\" + filename, @"Games_deobfuscated\Games\" +filename );

		}
	}

	private static bool Compare(string path1, string path2)
	{
		var lines1 = File.ReadAllLines(Path.Combine(baseFolder, path1));
		var lines2 = File.ReadAllLines(Path.Combine(baseFolder, path2));

		if(lines1.Length != lines2.Length)
		{
			Console.WriteLine($"Different number of lines: {path1} vs {path2}");
			return false;
		}

		for(int i=0; i < lines1.Length;++i)
		{
			if (lines1[i].Trim().Equals(lines2[i].Trim()) == false)
			{
				Console.WriteLine($"Difference for {Path.GetFileNameWithoutExtension(path1)}");
				Console.WriteLine(lines1[i].Trim());
				Console.WriteLine(lines2[i].Trim());
				Console.WriteLine("");
				return false;
			}
		}

		return true;
	}

	private static void Obfuscate()
	{
		Console.WriteLine($"Obfuscation in folder {baseFolder}");
		changer = new NameChanger(baseFolder);
		var dirs = Directory.EnumerateDirectories(baseFolder);
		foreach(var directory in dirs)
		{
			if(directory.Contains("_obfuscated"))
			{
				continue;
			}

			ObfuscateDirectory(directory);
		}

		Console.WriteLine($"Encoding size = {changer.EncodingSize}");
		changer.Save();
	}

	private static void DeObfuscate()
	{
		Console.WriteLine($"De-obfuscation in folder {baseFolder}");
		changer = new NameChanger(baseFolder);

		var dirs = Directory.EnumerateDirectories(baseFolder);
		foreach (var directory in dirs)
		{
			if (!directory.Contains("_obfuscated_n"))
				continue;

			DeObfuscateDirectory(directory);
		}
	}

	private static void ObfuscateDirectory(string path)
	{
		var dirObfuscated = path + "_obfuscated";
		if(Directory.Exists(dirObfuscated)==false)
		{
			Directory.CreateDirectory(dirObfuscated);
		}

		var dirs = Directory.EnumerateDirectories(path);
		foreach(var directory in dirs)
		{
			Console.WriteLine($"Obfuscation in directory: {directory}");
			var outputDirectory = Path.Combine(dirObfuscated, Path.GetFileName(directory));
			if(Directory.Exists(outputDirectory) == false)
			{
				Directory.CreateDirectory(outputDirectory);
			}

			var files = Directory.EnumerateFiles(directory);
			foreach(var file in files)
			{
				ObfuscateFile(file, outputDirectory);
			}
		}
	}

	private static void ObfuscateFile(string filePath, string outputDirectory)
	{
		var lines = File.ReadLines(filePath);
		StreamWriter writer = new StreamWriter(Path.Combine(outputDirectory, Path.GetFileName(filePath)));
		foreach(var line in lines)
		{
			if(line.Length > 0 && line[0] == ';')
			{
				continue;
			}

			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder currentWord = new StringBuilder();

			foreach (char c in line)
			{
				if(separators.Contains(c))
				{
					if(currentWord.Length > 0)
					{
						stringBuilder.Append(changer.Encode(currentWord.ToString()));
					}

					stringBuilder.Append(c);
					currentWord.Clear();
				}
				else
				{
					currentWord.Append(c);
				}
			}
			if (currentWord.Length > 0)
			{
				stringBuilder.Append(changer.Encode(currentWord.ToString()));
			}

			writer.WriteLine(stringBuilder.ToString());
		}

		writer.Close();
	}

	private static void DeObfuscateDirectory(string obfuscatedPath)
	{
		string originalDir = obfuscatedPath.Replace("_obfuscated", "_deobfuscated");

		if (!Directory.Exists(originalDir))
			Directory.CreateDirectory(originalDir);

		var dirs = Directory.EnumerateDirectories(obfuscatedPath);

		foreach (var directory in dirs)
		{
			Console.WriteLine($"De-obfuscation in directory: {directory}");

			var outputDirectory = Path.Combine(originalDir, Path.GetFileName(directory));

			if (!Directory.Exists(outputDirectory))
				Directory.CreateDirectory(outputDirectory);

			var files = Directory.EnumerateFiles(directory);

			foreach (var file in files)
			{
				DeObfuscateFile(file, outputDirectory);
			}
		}
	}

	private static void DeObfuscateFile(string filePath, string outputDirectory)
	{
		var lines = File.ReadLines(filePath);
		string outputPath = Path.Combine(outputDirectory, Path.GetFileName(filePath));

		using StreamWriter writer = new StreamWriter(outputPath);

		foreach (var line in lines)
		{
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder currentWord = new StringBuilder();

			foreach (char c in line)
			{
				if (separators.Contains(c))
				{
					if (currentWord.Length > 0)
					{
						string original = changer.Decode(currentWord.ToString());
						stringBuilder.Append(original);
					}

					stringBuilder.Append(c);
					currentWord.Clear();
				}
				else
				{
					currentWord.Append(c);
				}
			}

			if (currentWord.Length > 0)
			{
				string original = changer.Decode(currentWord.ToString());
				stringBuilder.Append(original);
			}

			writer.WriteLine(stringBuilder.ToString());
		}
	}
}