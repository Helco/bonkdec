// The IDCT was taken from https://github.com/russellklenk/libmega/blob/bcdd2f54fda2387bd5a287cc9375f8cc2cfba12a/imutils.cpp
// which was made available under the "Unlicense" license
// and adapted to C# and non-quantized coefficients
namespace Bonk;
using System;

internal static unsafe class IDCT8x8
{
    public static void idct(short* inp, short* dst, ReadOnlySpan<int> quantizers)
    {
        unchecked
        {
            const int DCTSIZE = 8;
            int* workspace = stackalloc int[64];
            int* wsp = workspace;

            // @note: intermediate values are stored in 32-bit integers in C
            // since overflow of signed integer values is technically undefined.

            for (int i = DCTSIZE; i > 0; --i)
            {
                // process columns from the input; write the result to workspace.
                int c0 = (inp[DCTSIZE * 0] * quantizers[DCTSIZE * 0]) >> 11;
                int d4 = (inp[DCTSIZE * 1] * quantizers[DCTSIZE * 1]) >> 11;
                int c2 = (inp[DCTSIZE * 2] * quantizers[DCTSIZE * 2]) >> 11;
                int d6 = (inp[DCTSIZE * 3] * quantizers[DCTSIZE * 3]) >> 11;
                int c1 = (inp[DCTSIZE * 4] * quantizers[DCTSIZE * 4]) >> 11;
                int d5 = (inp[DCTSIZE * 5] * quantizers[DCTSIZE * 5]) >> 11;
                int c3 = (inp[DCTSIZE * 6] * quantizers[DCTSIZE * 6]) >> 11;
                int d7 = (inp[DCTSIZE * 7] * quantizers[DCTSIZE * 7]) >> 11;
                int c4 = d4;
                int c5 = d5 + d6;
                int c7 = d5 - d6;
                int c6 = d7;
                int b4 = c4 + c5;
                int b5 = c4 - c5;
                int b6 = c6 + c7;
                int b7 = c6 - c7;
                int b0 = c0 + c1;
                int b1 = c0 - c1;
                int b2 = c2 + (c2 >> 2) + (c3 >> 1);
                int b3 = (c2 >> 1) - c3 - (c3 >> 2);
                int a4 = (b7 >> 2) + b4 + (b4 >> 2) - (b4 >> 4);
                int a7 = (b4 >> 2) - b7 - (b7 >> 2) + (b7 >> 4);
                int a5 = b5 - b6 + (b6 >> 2) + (b6 >> 4);
                int a6 = b6 + b5 - (b5 >> 2) - (b5 >> 4);
                int a0 = b0 + b2;
                int a3 = b0 - b2;
                int a1 = b1 + b3;
                int a2 = b1 - b3;
                wsp[DCTSIZE * 0] = a0 + a4;
                wsp[DCTSIZE * 1] = a1 + a5;
                wsp[DCTSIZE * 2] = a2 + a6;
                wsp[DCTSIZE * 3] = a3 + a7;
                wsp[DCTSIZE * 4] = a3 - a7;
                wsp[DCTSIZE * 5] = a2 - a6;
                wsp[DCTSIZE * 6] = a1 - a5;
                wsp[DCTSIZE * 7] = a0 - a4;
                wsp++;
                inp++;
                quantizers = quantizers.Slice(1);
            }

            wsp = workspace;
            for (int i = 0; i < DCTSIZE; ++i)
            {
                // now process rows from the work array; write the result to dst.
                int c0 = wsp[0];
                int d4 = wsp[1];
                int c2 = wsp[2];
                int d6 = wsp[3];
                int c1 = wsp[4];
                int d5 = wsp[5];
                int c3 = wsp[6];
                int d7 = wsp[7];
                int c4 = d4;
                int c5 = d5 + d6;
                int c7 = d5 - d6;
                int c6 = d7;
                int b4 = c4 + c5;
                int b5 = c4 - c5;
                int b6 = c6 + c7;
                int b7 = c6 - c7;
                int b0 = c0 + c1;
                int b1 = c0 - c1;
                int b2 = c2 + (c2 >> 2) + (c3 >> 1);
                int b3 = (c2 >> 1) - c3 - (c3 >> 2);
                int a4 = (b7 >> 2) + b4 + (b4 >> 2) - (b4 >> 4);
                int a7 = (b4 >> 2) - b7 - (b7 >> 2) + (b7 >> 4);
                int a5 = b5 - b6 + (b6 >> 2) + (b6 >> 4);
                int a6 = b6 + b5 - (b5 >> 2) - (b5 >> 4);
                int a0 = b0 + b2;
                int a3 = b0 - b2;
                int a1 = b1 + b3;
                int a2 = b1 - b3;
                dst[0] = (short)(a0 + a4);
                dst[1] = (short)(a1 + a5);
                dst[2] = (short)(a2 + a6);
                dst[3] = (short)(a3 + a7);
                dst[4] = (short)(a3 - a7);
                dst[5] = (short)(a2 - a6);
                dst[6] = (short)(a1 - a5);
                dst[7] = (short)(a0 - a4);
                dst += DCTSIZE;
                wsp += DCTSIZE;
            }
        }
    }
}
