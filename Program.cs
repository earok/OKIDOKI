using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OKIDOKI
{
	class Program
	{
		const int DefaultHZ = 7600;

		public static int ExportedSamples;

		static void Main(string[] args)
		{
			try
			{
				Functions.ComputeTables();

				Console.WriteLine("OKIDOKI 6295 sample ripper");
				Console.WriteLine("Rip mode assumes locations of all samples are at top of ROM file");

				if (args.Length == 0)
				{
					throw new Exception("File name must be specified in command line, otherwise drag+drop file here");
				}

				var fileName = args[0];
				Console.WriteLine("Import mode (i) or Rip mode (any key)?");
				var keyInfo = Console.ReadKey();
				Console.WriteLine("");
				if (keyInfo.Key == ConsoleKey.I)
				{
					ImportMode(fileName);
				}
				else
				{
					RipMode(fileName);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			Console.WriteLine("Press any key to exit");
			Console.ReadKey();
		}

		private static void ImportMode(string fileName)
		{
			Console.WriteLine("Looking for suitably named WAV files to import into file");
			Console.WriteLine("ie [STARTOFFSET]-[ENDOFFSET].wav");
			var bytes = File.ReadAllBytes(fileName);

			var path = Path.GetDirectoryName(fileName);
			if (string.IsNullOrEmpty(path)) path = ".";
			foreach (var file in Directory.GetFiles(path))
			{
				if (!file.ToLower().EndsWith(".wav")) continue;
				var wavFile = Path.GetFileNameWithoutExtension(file).ToLower();

				var split = wavFile.Split("-");
				if (split.Length != 2) continue;
				Console.WriteLine("Importing " + file);

				var startOffset = 0;
				var endOffset = 0;
				if (!int.TryParse(split[0], out startOffset)) continue;
				if (!int.TryParse(split[1], out endOffset)) continue;

				using var reader = new WaveFileReader(file);
				var format = reader.WaveFormat;
				if (format.BitsPerSample != 16) throw new Exception("WAV file must be 16 bit: " + file);
				if (format.Channels != 1) throw new Exception("WAV file must be mono: " + file);

				var buffer = new Byte[reader.Length];
				reader.Read(buffer, 0, buffer.Length);

				//Convert to 16 bit
				var samples = new short[buffer.Length / 2];
				for (var i = 0; i < buffer.Length; i += 2)
				{
					samples[i / 2] = (short)(buffer[i + 1] << 8 | buffer[i]);
				}

				//Read
				Functions.Reset();
				for (var i = 0; i < samples.Length; i += 2)
				{
					//Read two samples per byte, converting from 16bit to 12bit
					var sample1 = Functions.encode((short)(samples[i] >> 4));
					var sample2 = Functions.encode((short)(samples[i + 1] >> 4));

					bytes[startOffset] = (byte)((sample1 << 4) | sample2);
					startOffset += 1;
					if (startOffset > endOffset) break;
				}

			}

			File.WriteAllBytes(fileName, bytes);
		}

		private static void RipMode(string fileName)
		{
			var fileBytes = File.ReadAllBytes(fileName);

			Console.WriteLine("HZ of samples? (Default: " + DefaultHZ + ")");
			var hzString = Console.ReadLine();

			int hz;
			if (int.TryParse(hzString, out hz) == false)
			{
				hz = DefaultHZ;
			}
			var samples = GetSampleMetaData(fileBytes);

			if (VerifySamples(samples) == false)
			{
				Console.WriteLine("Error processing sample table. Attempting autorip");
				samples = GetSampleMetaData_AutoRip(fileBytes);
			}

			foreach (var sample in samples)
			{
				sample.Export(hz, fileBytes);
			}

			Console.WriteLine("Success! Converted " + ExportedSamples + " samples");
		}

		private static bool VerifySamples(List<OKISample> samples)
		{
			samples.Sort(OKISample.Sorter);
			for (var i = 0; i < samples.Count - 1; i++)
			{
				var firstSample = samples[i];

				for (var j = i + 1; j < samples.Count; j++)
				{
					var secondSample = samples[j];
					if (secondSample.StartAddress <= firstSample.EndAddress)
					{
						return false;
					}
				}
			}

			return true;
		}

		private static List<OKISample> GetSampleMetaData_AutoRip(byte[] fileBytes)
		{
			var result = new List<OKISample>();

			bool isReading = false;
			int startAddress = 0;
			int i = 0;

			while (i < fileBytes.Length - 8)
			{
				if (Functions.EmptyLine(fileBytes, i) == false)
				{
					if (isReading == false)
					{
						//Start reading this sample
						isReading = true;
						startAddress = i;
					}
				}
				else
				{
					if (isReading)
					{
						result.Add(new OKISample() { StartAddress = startAddress, EndAddress = i - 1 });
						isReading = false;
					}
				}
				i += 8;
			}

			if (isReading)
			{
				result.Add(new OKISample() { StartAddress = startAddress, EndAddress = fileBytes.Length - 1 });
			}

			return result;
		}

		private static List<OKISample> GetSampleMetaData(byte[] fileBytes)
		{
			var result = new List<OKISample>();

			int maxSampleTableOffset = fileBytes.Length - 8;
			for (var i = 0; i < maxSampleTableOffset; i += 8)
			{
				//7th and 8th bytes must be 00
				if (fileBytes[i + 6] > 0 || fileBytes[i + 7] > 0) continue;

				var startAddress = Functions.Get24BitAddress(fileBytes, i);
				var endAddress = Functions.Get24BitAddress(fileBytes, i + 3);

				if (endAddress > startAddress)
				{
					//Invalid if end address is greater than or equal to file length
					if (endAddress >= fileBytes.Length) continue;

					//Is this one already in the list?
					if (result.Any(p => p.StartAddress == startAddress && p.EndAddress == endAddress))
					{
						continue;
					}

					//Assume that we've got a valid sample
					result.Add(new OKISample() { TableAddress = i, StartAddress = startAddress, EndAddress = endAddress });

					//Use the start address of this sample to set the end of the sample address table
					if (startAddress < maxSampleTableOffset)
					{
						maxSampleTableOffset = startAddress;
					}
				}
			}
			return result;
		}
	}
}
