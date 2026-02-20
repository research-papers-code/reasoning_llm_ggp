namespace ObfuscatorGGP
{
	public class DirectoryComparer
	{
		public void Run(string baseDirectory)
		{
			if (!Directory.Exists(baseDirectory))
			{
				Console.WriteLine($"Base directory does not exist: {baseDirectory}");
				return;
			}

			var allDirs = Directory.GetDirectories(baseDirectory, "*", SearchOption.TopDirectoryOnly);

			foreach (var normalDir in allDirs.Where(d => !Path.GetFileName(d).Contains("obfuscated")))
			{
				string dirName = Path.GetFileName(normalDir);
				string obfuscatedDir = Path.Combine(baseDirectory, dirName + "_obfuscated");

				if (!Directory.Exists(obfuscatedDir))
				{
					Console.WriteLine($"No obfuscated counterpart for: {dirName}");
					continue;
				}

				Console.WriteLine($"Comparing:");
				Console.WriteLine($"  Normal:     {normalDir}");
				Console.WriteLine($"  Obfuscated: {obfuscatedDir}");
				Console.WriteLine();

				CompareFileTrees(normalDir, obfuscatedDir);
			}
		}

		private void CompareFileTrees(string normalDir, string obfuscatedDir)
		{
			var allNormalFiles = Directory.GetFiles(normalDir, "*.*", SearchOption.AllDirectories);

			foreach (var normalFile in allNormalFiles)
			{
				string relativePath = Path.GetRelativePath(normalDir, normalFile);
				string obFile = Path.Combine(obfuscatedDir, relativePath);

				if (!File.Exists(obFile))
				{
					Console.WriteLine($"Missing in obfuscated: {relativePath}");
					continue;
				}

				if (!FilesAreEqual(normalFile, obFile))
				{
					Console.WriteLine($"DIFFERENCE: {relativePath}");
				}
			}
		}

		private bool FilesAreEqual(string file1, string file2)
		{
			var info1 = new FileInfo(file1);
			var info2 = new FileInfo(file2);

			if (info1.Length != info2.Length)
				return false;

			const int bufferSize = 16 * 1024;

			using var fs1 = File.OpenRead(file1);
			using var fs2 = File.OpenRead(file2);

			var buffer1 = new byte[bufferSize];
			var buffer2 = new byte[bufferSize];

			int read1, read2;
			do
			{
				read1 = fs1.Read(buffer1, 0, bufferSize);
				read2 = fs2.Read(buffer2, 0, bufferSize);

				if (read1 != read2)
					return false;

				for (int i = 0; i < read1; i++)
				{
					if (buffer1[i] != buffer2[i])
						return false;
				}

			} while (read1 > 0);

			return true;
		}
	}

}
