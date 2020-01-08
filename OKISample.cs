using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;

namespace OKIDOKI
{
    public class OKISample
    {
        public int StartAddress;
        public int EndAddress;
        public int TableAddress;

        public int Size
        {
            get
            {
                return EndAddress - StartAddress + 1;
            }
        }

        internal void Export(int hz, byte[] fileBytes)
        {
            var samples = new List<short>();
            if (isRedundant) return;

            Functions.Reset();

            for(var i = StartAddress;i <= EndAddress;i++)
            {
                //First nibble
                samples.Add(Functions.Clock(fileBytes[i] >> 4));

                //Second nibble
                samples.Add(Functions.Clock(fileBytes[i] & 0xf));
            }

            var fileName = StartAddress + "-" + EndAddress + ".wav";
            var waveFormat = new WaveFormat(hz, 16, 1);

            using (var writer = new WaveFileWriter(fileName, waveFormat))
            {
                writer.WriteSamples(samples.ToArray(), 0, samples.Count);
            }

            Program.ExportedSamples += 1;
        }

        internal static int Sorter(OKISample x, OKISample y)
        {
            if (x.StartAddress == y.StartAddress) return y.EndAddress - x.EndAddress;
            return x.StartAddress - y.StartAddress;
        }
    }
}
