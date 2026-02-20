namespace ObfuscatorGGP
{
	public class NameChanger
	{
		private string ENCODER_FILE = "..\\encoding.txt";
		private string NAMES_FILE = "names.txt";

		private readonly string baseFolder;

		private Dictionary<string, string> encoder = new Dictionary<string, string>();
		private Dictionary<string, string> decoder = new Dictionary<string, string>();
		private List<string> names = new List<string>();

		public int EncodingSize => encoder.Count;

		public NameChanger(string baseFolder)
		{
			this.baseFolder = baseFolder;
			Load();
		}

		private void Add(string name, string encoded_name)
		{
			encoder.Add(name, encoded_name);
			decoder.Add(encoded_name, name);
		}

		public string Encode(string name)
		{
			if (name.Length == 0 || name.StartsWith(';'))
			{
				return name;
			}

			string encoded_name;
			if (encoder.ContainsKey(name) == false)
			{
				encoded_name = names.First();
				if (name.StartsWith('?'))
				{
					encoded_name = '?' + encoded_name;
				}

				names.RemoveAt(0);
				Add(name, encoded_name);
				return encoded_name;
			}

			return encoder[name];
		}

		public string Decode(string name)
		{
			if (name.Length == 0 || name.StartsWith(";"))
			{
				return name;
			}

			if (decoder.ContainsKey(name) == false)
				return name;

			return decoder[name];
		}

		public void Save()
		{
			string path = Path.Combine(baseFolder, ENCODER_FILE);
			StreamWriter writer = new StreamWriter(path);
			foreach(var encoding in encoder)
			{
				writer.WriteLine($"{encoding.Key} {encoding.Value}");
			}

			writer.Close();
		}

		public void Load()
		{
			string namesPath = Path.Combine(baseFolder, NAMES_FILE);
			if(Path.Exists(namesPath))
			{
				names.AddRange(File.ReadAllLines(namesPath).ToList());
			}

			string path = Path.Combine(baseFolder, ENCODER_FILE);
			var lines = File.ReadLines(path);
			foreach (string line in lines)
			{
				var nameToEncoding = line.Split(' ');
				if (encoder.ContainsKey(nameToEncoding[0]))
				{
					Console.WriteLine($"Error! Duplicate encoding for: {nameToEncoding[0]}");
				}
				names.Remove(nameToEncoding[0]);
				names.Remove(nameToEncoding[1]);
				Add(nameToEncoding[0], nameToEncoding[1]);
			}

			Console.WriteLine($"Encoding contains {encoder.Count} entries.");
		}
	}
}
