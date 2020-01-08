//https://github.com/mamedev/mame/blob/master/src/devices/sound/okiadpcm.cpp
//https://github.com/dreamflyforever/vox/blob/master/adpcm.c

using System;
using System.Collections.Generic;
using System.Text;

namespace OKIDOKI
{
    public static class Functions
    {
        static int[] s_diff_lookup = new int[49 * 16];
        static int m_signal;
        static int m_step;
        static int[] s_index_shift = new int[] { -1, -1, -1, -1, 2, 4, 6, 8 };

        internal static void ComputeTables()
        {
            // nibble to bit map
            var nbl2bit = new int[16, 4]
            {
                { 1, 0, 0, 0}, { 1, 0, 0, 1}, { 1, 0, 1, 0}, { 1, 0, 1, 1},
                { 1, 1, 0, 0}, { 1, 1, 0, 1}, { 1, 1, 1, 0}, { 1, 1, 1, 1},
                {-1, 0, 0, 0}, {-1, 0, 0, 1}, {-1, 0, 1, 0}, {-1, 0, 1, 1},
                {-1, 1, 0, 0}, {-1, 1, 0, 1}, {-1, 1, 1, 0}, {-1, 1, 1, 1}
            };

            // loop over all possible steps
            for (int step = 0; step <= 48; step++)
            {
                // compute the step value
                int stepval = (int)Math.Floor(16.0 * Math.Pow(11.0 / 10.0, (double)step));

                // loop over all nibbles and compute the difference
                for (int nib = 0; nib < 16; nib++)
                {
                    s_diff_lookup[step * 16 + nib] = nbl2bit[nib, 0] *
                        (stepval * nbl2bit[nib, 1] +
                            stepval / 2 * nbl2bit[nib, 2] +
                            stepval / 4 * nbl2bit[nib, 3] +
                            stepval / 8);
                }
            }
        }


        internal static int Get24BitAddress(byte[] fileBytes, int i)
        {
            return fileBytes[i] << 16
                | fileBytes[i + 1] << 8
                | fileBytes[i + 2];
        }

        public static void Reset()
        {
            m_signal = -2;
            m_step = 0;
        }

        public static short Clock(int nibble)
        {
            // update the signal
            m_signal += s_diff_lookup[m_step * 16 + (nibble & 15)];

            // clamp to the maximum
            if (m_signal > 2047)
                m_signal = 2047;
            else if (m_signal < -2048)
                m_signal = -2048;

            // adjust the step size and clamp
            m_step += s_index_shift[nibble & 7];
            if (m_step > 48)
                m_step = 48;
            else if (m_step < 0)
                m_step = 0;

            // return the signal
            return (short)(m_signal * 16);
        }

        internal static bool EmptyLine(byte[] fileBytes, int i)
        {
            for (var j = 0; j < 8; j++)
            {
                if (fileBytes[i + j] > 0 && fileBytes[i + j] < 255) return false;
            }
            return true;
        }
    }
}
