using System;
using System.Collections.Generic;
using System.IO;

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
                Console.WriteLine("Assumes locations of all samples are at top of ROM file");

                if (args.Length == 0)
                {
                    throw new Exception("File name must be specified in command line, otherwise drag+drop file here");
                }

                var fileBytes = File.ReadAllBytes(args[0]);

                Console.WriteLine("HZ of samples? (Default: " + DefaultHZ + ")");
                var hzString = Console.ReadLine();

                int hz;
                if (!int.TryParse(hzString, out hz))
                {
                    hz = DefaultHZ;
                }
                var samples = GetSampleMetaData(fileBytes);
                samples.Sort(OKISample.Sorter);

                var sampleTableError = false;
                for (var i = 0; i < samples.Count - 1; i++)
                {
                    var firstSample = samples[i];

                    for (var j = i + 1; j < samples.Count; j++)
                    {
                        var secondSample = samples[j];
                        if (secondSample.StartAddress <= firstSample.EndAddress)
                        {
                            sampleTableError = true;
                        }
                    }
                }

                if (sampleTableError)
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Console.ReadKey();
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
                    if(isReading)
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
