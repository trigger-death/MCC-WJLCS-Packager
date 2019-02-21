/* ================================
 * Program:  WJLCS.Packager
 * Author:   Robert Jordan
 * Version:  v1.0.0
 * Updated:  Dec 9th 2018
 */

using System;
using System.IO;
using System.Reflection;

namespace WJLCS.Packager {
	class Program {

		#region Art Constants

		const string WJLCSArt = @"
 _        _ ______ _      ____  ____   
| |      | |___   | |    / ___|/ ___|  
| |  __  | |   | || |   / /   / /___   
| | /  \ | |_  | || |   | |   \____ \  
\ \/ /\ \/ / |_/ /| |___\ \___ ___/ /  
 \__/  \__/\____/ |_____|\____|____/   ";
		const string PKGRArt = @"
 ____  _   __ ____ ____    
|  _ \| | / // ___|  _ \   
| |_| | |/ // / __| |_| |  
|  __/|    || | \ \    /   
| |   | |\ \\ \_| | |\ \   
|_|   |_| \_\\____|_| \_\  ";
		const string BoxArt = @"
          
 /\______/
/ /#####/|
\/#####/ |
 |     | /
 |_____|/ ";

		#endregion

		#region Constants
		
		const string CompactTextFile = "wjlcs-pkgr-txt.rtf";
		const string CompactBinFile = "wjlcs-pkgr-bin.rtf";
		const string CompactTextSignature = "WJLCSTXT";
		const string CompactBinSignature = "WJLCSBIN";

		#endregion

		#region Package-Specific Properties
		
		static string NormalizedOutputDir { get; set; }
		static bool IncludeRootFiles { get; set; } = true;
		static string CurrentDirectory { get; } = Directory.GetCurrentDirectory();

		#endregion

		#region Package+Unpackage Properties
		
		static string InputPath { get; set; } = ".";
		static string OutputPath { get; set; } = "Output";
		static int TextFileCount { get; set; } = 0;
		static int BinFileCount { get; set; } = 0;
		static int FileCount { get; set; } = 0;

		#endregion

		#region Entry Point

		static void Main(string[] args) {
			PrintTitle();

			bool unpackage = ReadYesNo("Run Unpackager", true);
			if (unpackage) {
				RunUnpackager();
			}
			else {
				RunPackager();
			}
			
			Console.ResetColor();
			Console.Write("Press enter to exit... ");
			Console.ReadLine();
		}

		#endregion

		#region Run (Un)Packager

		static void RunPackager() {

			// Read settings
			InputPath         = ReadInput("   Search Path", "*");
			OutputPath        = ReadInput("   Output Path", ".submission");
			IncludeRootFiles  = ReadYesNo("Add Root Files", IncludeRootFiles);

			OutputPath = OutputPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
			if (!Path.IsPathRooted(OutputPath)) {
				OutputPath = Path.Combine(CurrentDirectory, OutputPath);
			}
			NormalizedOutputDir = NormalizePath(OutputPath);

			// Clear the output directory
			DirectoryInfo dirInfo = new DirectoryInfo(OutputPath);
			if (dirInfo.Exists) {
				dirInfo.Delete(true);
			}
			Directory.CreateDirectory(OutputPath);

			Console.WriteLine();
			
			RunPackagerCompact();

			// Save helper files
			SaveEmbeddedFile("WJLCS.Packager.cs", "WJLCS.Packager.cs.rtf");
			SaveEmbeddedFile("WJLCS.Packager.csproj", "WJLCS.Packager.csproj.rtf");
			SaveEmbeddedFile("unpack-instructions.rtf", "unpack-instructions.rtf");

			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine($"Finished packaging {FileCount} file(s) to \"{OutputPath}\"");
			Console.ResetColor();
		}

		static void RunUnpackager() {

			// Read settings
			InputPath  = ReadInput("    Input Path", InputPath);
			OutputPath = ReadInput("   Output Path", OutputPath);
			
			// Clear the output directory
			DirectoryInfo dirInfo = new DirectoryInfo(OutputPath);
			if (dirInfo.Exists) {
				dirInfo.Delete(true);
			}
			Directory.CreateDirectory(OutputPath);

			Console.WriteLine();

			RunUnpackagerCompact();

			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine($"Finished unpackaging {FileCount} file(s) to \"{OutputPath}\"");
			Console.ResetColor();
		}

		#endregion

		#region Run (Un)Packager Compact

		static void RunPackagerCompact() {
			using (var binStream = File.Create(Path.Combine(OutputPath, CompactBinFile)))
			using (var textWriter = File.CreateText(Path.Combine(OutputPath, CompactTextFile)))
			using (var binWriter = new BinaryWriter(binStream)) {
				// Make sure the files are empty
				binWriter.BaseStream.SetLength(0);
				textWriter.BaseStream.SetLength(0);

				// Write the signature and the position for the file count
				binWriter.Write(CompactBinSignature.ToCharArray());
				long binCountPos = binWriter.BaseStream.Position;
				binWriter.Write(0);
				textWriter.WriteLine(CompactTextSignature);
				// Flush so the stream position is updated
				textWriter.Flush();
				long textCountPos = textWriter.BaseStream.Position;
				WriteTextVariable(textWriter, "FileCount", new string(' ', 16));
				
				// Enumerate all files and append them to the binary and text files
				PackageDirectory(CurrentDirectory, true, textWriter, binWriter);

				// This flush is required to prevent the files from
				// getting mixed up when we go back to change FileCount
				textWriter.Flush();

				// Go back and write the file count in the space we made room at
				binWriter.BaseStream.Position = binCountPos;
				binWriter.Write(BinFileCount);
				textWriter.BaseStream.Position = textCountPos;
				WriteTextVariable(textWriter, "FileCount", TextFileCount.ToString().PadRight(16));
				
				// Flush to make sure all the changes are pushed
				textWriter.Flush();
			}
		}
		
		static void RunUnpackagerCompact() {
			string binPath = Path.Combine(InputPath, CompactBinFile);
			string textPath = Path.Combine(InputPath, CompactTextFile);
			if (File.Exists(binPath)) {
				using (var binReader = new BinaryReader(File.OpenRead(binPath))) {
					// Confirm the signature
					string sig = new string(binReader.ReadChars(CompactBinSignature.Length));
					if (sig != CompactBinSignature)
						throw new FormatException("Invalid compact binary file!");

					// Read the number of appended files
					int binCount = binReader.ReadInt32();

					// Loop through the appended binary files one-by-one and unpackage them
					for (int i = 0; i < binCount; i++) {
						UnpackageCompactFile(true, null, binReader);
					}
				}
			}
			else {
				PrintError($"\"{CompactBinFile}\" file not found in \"{InputPath}\"!");
			}
			if (File.Exists(textPath)) {
				using (var textReader = new StreamReader(textPath)) {
					// Confirm the signature
					string sig = textReader.ReadLine();
					if (sig != CompactTextSignature)
						throw new FormatException("Invalid compact text file!");

					// This loop is in place for future additions to variables
					int textCount = 0;
					for (int i = 0; i < 1; i++) {
						switch (ReadTextVariable(textReader, out string value)) {
						case "FileCount":
							textCount = int.Parse(value);
							break;
						default:
							throw new Exception("Failed to read Packager Info line!");
						}
					}

					// Loop through the appended text files one-by-one and unpackage them
					for (int i = 0; i < textCount; i++) {
						UnpackageCompactFile(false, textReader, null);
					}
				}
			}
			else {
				PrintError($"\"{CompactTextFile}\" file not found in \"{InputPath}\"!");
			}
		}

		#endregion
		
		#region Read/Write Packager Line

		// Simple format of colon-separated NAME: TRIMMED VALUE

		static void WriteTextVariable(StreamWriter writer, string name, string value) {
			writer.WriteLine($"{name}: {value}");
		}
		static string ReadTextVariable(StreamReader reader, out string value) {
			string line = reader.ReadLine();
			int colonIndex = line.IndexOf(":");
			if (colonIndex == -1) {
				value = null;
				return null;
			}
			value = line.Substring(colonIndex + 2).Trim();
			return line.Substring(0, colonIndex);
		}

		#endregion

		#region Package Directory

		static void PackageDirectory(string directory, bool root, StreamWriter textWriter = null, BinaryWriter binWriter = null) {
			if (!root || IncludeRootFiles) {
				// Include files
				foreach (string file in Directory.EnumerateFiles(directory)) {
					if (!ShouldExclude(file, true))
						PackageCompactFile(file, textWriter, binWriter);
				}
			}

			// Recurse subdirectories, first root directory uses pattern
			foreach (string subdirectory in Directory.EnumerateDirectories(directory, root ? InputPath : "*")) {
				if (!ShouldExclude(subdirectory, false))
					PackageDirectory(subdirectory, false, textWriter, binWriter);
			}
		}
		static bool ShouldExclude(string path, bool isFile) {
			string name = Path.GetFileName(path).ToLower();
			if (name.StartsWith(".") || name.StartsWith("~") || name == "wjlcs.packager.exe") {
				return true;
			}
			else if (!isFile) {
				return (name == "bin" ||
						name == "obj" ||
						name == "packages" ||
						name == "build" ||   // Java
						name == "private" || // NetBeans
						(NormalizedOutputDir != null && NormalizePath(path) == NormalizedOutputDir));
			}
			return false;
		}
		static string NormalizePath(string path) {
			return Path.GetFullPath(new Uri(path, UriKind.RelativeOrAbsolute).AbsolutePath)
					   .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
					   .ToUpperInvariant();
		}

		#endregion

		#region (Un)Package Compact File

		static void PackageCompactFile(string inputFile, StreamWriter textWriter, BinaryWriter binWriter) {
			string relativeFile;
			relativeFile = inputFile.Substring(CurrentDirectory.Length).Trim('\\', '/'); // Eliminate relative path
			Console.ForegroundColor = ConsoleColor.White;

			bool isBinary = false;
			string text = "";
			try {
				text = File.ReadAllText(inputFile);
				foreach (char c in text) {
					if (c >= '\0' && c < ' ' && c != '\t' && c != '\n' && c != '\r') {
						isBinary = true;
						break;
					}
				}
			} catch (IOException) {
			} catch (Exception) {
				isBinary = true;
			}
			if (isBinary) {
				Console.Write("Packaged Bin: ");
				Console.ForegroundColor = ConsoleColor.Cyan;
				byte[] data = File.ReadAllBytes(inputFile);
				using (var stream = File.OpenRead(inputFile)) {
					binWriter.Write(relativeFile);
					binWriter.Write(stream.Length);
					stream.CopyTo(binWriter.BaseStream);
				}
				BinFileCount++;
			}
			else {
				Console.Write("Packaged Txt: ");
				Console.ForegroundColor = ConsoleColor.Yellow;
				WriteTextVariable(textWriter, "RelativeFile", relativeFile);
				WriteTextVariable(textWriter, "Length", text.Length.ToString());
				textWriter.Write(text);
				TextFileCount++;
			}
			Console.WriteLine(relativeFile);
			FileCount++;

			Console.ResetColor();
		}
		static void UnpackageCompactFile(bool isBinary, StreamReader textReader, BinaryReader binReader) {
			Console.ForegroundColor = ConsoleColor.White;
			string relativeFile = null;
			if (isBinary) {
				relativeFile = binReader.ReadString();
				long size = binReader.ReadInt64();

				string relativeDir = Path.GetDirectoryName(relativeFile);
				if (!string.IsNullOrEmpty(relativeDir) && !Directory.Exists(Path.Combine(OutputPath, relativeDir)))
					Directory.CreateDirectory(Path.Combine(OutputPath, relativeDir));

				using (var stream = File.Create(Path.Combine(OutputPath, relativeFile))) {
					stream.SetLength(0);
					byte[] buffer = new byte[(short.MaxValue + 1) * 2];
					while (size > 0) {
						int length = (int) Math.Min(size, buffer.LongLength);
						binReader.BaseStream.Read(buffer, 0, length);
						stream.Write(buffer, 0, length);
						size -= length;
					}
				}
				Console.Write("Unpackeged Bin: ");
				Console.ForegroundColor = ConsoleColor.Cyan;
				BinFileCount++;
			}
			else {
				int length = -1;
				for (int i = 0; i < 2; i++) {
					switch (ReadTextVariable(textReader, out string value)) {
					case "RelativeFile":
						relativeFile = value;
						break;
					case "Length":
						length = int.Parse(value);
						break;
					default:
						throw new Exception("Failed to read Packaged file line!");
					}
				}
				string relativeDir = Path.GetDirectoryName(relativeFile);
				if (!string.IsNullOrEmpty(relativeDir) && !Directory.Exists(Path.Combine(OutputPath, relativeDir)))
					Directory.CreateDirectory(Path.Combine(OutputPath, relativeDir));

				char[] buffer = new char[length];
				textReader.ReadBlock(buffer, 0, length);
				string text = new string(buffer);
				File.WriteAllText(Path.Combine(OutputPath, relativeFile), text);
				Console.Write("Unpackeged Txt: ");
				Console.ForegroundColor = ConsoleColor.Yellow;
				TextFileCount++;
			}
			Console.WriteLine(relativeFile);
			FileCount++;
		}

		#endregion
		
		#region Printing & I/O

		static void PrintTitle() {
			string[][] art = new string[3][] {
				WJLCSArt.Split(new string[]{ "\r\n", "\n" }, StringSplitOptions.None),
				PKGRArt.Split(new string[]{ "\r\n", "\n" }, StringSplitOptions.None),
				BoxArt.Split(new string[]{ "\r\n", "\n" }, StringSplitOptions.None),
			};
			ConsoleColor[] colors = new ConsoleColor[3] {
				ConsoleColor.Magenta,
				ConsoleColor.Green,
				ConsoleColor.DarkYellow,
			};
			int artCount = art.Length;
			int lineCount = art[0].Length;
			// Skip the first empty line used for more easily displaying the art in code.
			for (int l = 1; l < lineCount; l++) {
				for (int i = 0; i < artCount; i++) {
					Console.ForegroundColor = colors[i];
					Console.Write(art[i][l]);
				}
				Console.WriteLine();
			}
			Console.ResetColor();
			Console.WriteLine();
		}
		static void PrintError(string message) {
			Console.ForegroundColor =  ConsoleColor.Red;
			Console.WriteLine(message);
			Console.ResetColor();
		}
		static void PrintWatermark(string watermark) {
			ConsoleColor lastColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkGray;
			int left = Console.CursorLeft;
			int top = Console.CursorTop;
			Console.Write(watermark);
			Console.CursorLeft = left;
			Console.CursorTop = top;
			Console.ForegroundColor = lastColor;
		}
		static string ReadInput(string name, string defaultValue = null) {
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.Write($"{name}: ");
			if (defaultValue != null)
				PrintWatermark(defaultValue);
			Console.ForegroundColor = ConsoleColor.White;
			string input = Console.ReadLine().Trim();
			Console.ResetColor();
			if (defaultValue != null && string.IsNullOrEmpty(input))
				return defaultValue;
			if (input.StartsWith("\"") && input.EndsWith("\"") && input.Length >= 2)
				input = input.Substring(1, input.Length - 2);
			return input;
		}
		static bool ReadYesNo(string name, bool? defaultValue = null) {
			string input = ReadInput(name, (defaultValue.HasValue ? (defaultValue.Value ? "yes" : "no") : null)).ToLower();
			return (input == "yes" || input == "y");
		}

		#endregion

		#region SaveEmbeddedFile

		static void SaveEmbeddedFile(string resourcePath, string file) {
			using (var inStream = Assembly.GetCallingAssembly().GetManifestResourceStream($"WJLCS.Packager.{resourcePath}")) {
				if (inStream != null) {
					using (FileStream outStream = File.Create(Path.Combine(OutputPath, file)))
						inStream.CopyTo(outStream);
				}
			}
		}

		#endregion
	}
}
