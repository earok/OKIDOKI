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
        static int m_last;
        static int m_stepindex;
        static int[] s_index_shift = new int[] { -1, -1, -1, -1, 2, 4, 6, 8 };
        static int[] step_size = new int[] { 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41,
            45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173,
             190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658,
             724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552 };

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
            m_stepindex = 0;
            m_last = 0;
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

        internal static byte encode(short samp)
        {
            var diff = 0;
            var SS = step_size[m_stepindex];

            byte code = 0x00;
            if ((diff = samp - m_last) < 0)
            {
                code = 0x08;
            }

            var E = diff < 0 ? -diff : diff;
            if (E >= SS)
            {
                code |= 0x04;
                E -= SS;
            }
            if (E >= SS / 2)
            {
                code |= 0x02;
                E -= SS / 2;
            }
            if (E >= SS / 4)
            {
                code |= 0x01;
            }

            /*
            * Use the decoder to set the estimate of last sample.
            * It also will adjust the step_index for us.
            */
            m_last = decode(code);
            return code;
        }

        internal static int decode(int code)
        {
            var SS = step_size[m_stepindex];
            var E = SS / 8;

            if ((code & 0x01) > 0)
            {
                E += SS / 4;
            }
            if ((code & 0x02) > 0)
            {
                E += SS / 2;
            }
            if ((code & 0x04) > 0)
            {
                E += SS;
            }

            int diff = ((code & 0x08) > 0) ? -E : E;
            int samp = m_last + diff;

            /*
            *  Clip the values to +(2^11)-1 to -2^11. (12 bits 2's
            *  compelement)
            *  Note: previous version errantly clipped at +2048, which could
            *  cause a 2's complement overflow and was likely the source of
            *  clipping problems in the previous version.  Thanks to Frank
            *  van Dijk for the correction.  TLB 3/30/04
            */
            if (samp > 2047)
            {
                samp = 2047;
            }
            if (samp < -2048)
            {
                samp = -2048;
            }

            m_last = samp;
            m_stepindex += s_index_shift[code & 0x7];
            if (m_stepindex < 0) m_stepindex = 0;
            if (m_stepindex > 48) m_stepindex = 48;
            return (samp);
        }
    }
}
