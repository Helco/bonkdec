namespace Bonk;
using System;
using System.Runtime.CompilerServices;

internal static unsafe class IDCT8x8
{
    private const int C1 = 2217;
    private const int C2 = 2896;
    private const int C3 = 3784;
    private const int C4 = -5352;

    public static void idct(short* coeffs, short* dst, ReadOnlySpan<int> quantizers)
    {
        int c0, c1, c2, c3, c4, c5, c6, c7;
        int v13, v14, v16, v17, v21, v22, v23, v24, v25, v26, v32,
            v34, v35, v36, v37, v39,  v43, v44, v45, v46, v47, v48,
            v49, v50,  v52,  v56, v58, v60, v61, v62, v64, v66, v67;

        int* tmp = stackalloc int[64];
        int* workspace = tmp;
        for (int i = 0; i < 8; i++)
        {
            if (coeffs[8] != 0 || coeffs[16] != 0 || coeffs[24] != 0 || coeffs[32] != 0 || coeffs[40] != 0 || coeffs[48] != 0 || coeffs[56] != 0)
            {
                c0 = (quantizers[0] * coeffs[0]) >> 11;
                c1 = (quantizers[8] * coeffs[8]) >> 11;
                c2 = (quantizers[16] * coeffs[16]) >> 11;
                c3 = (quantizers[24] * coeffs[24]) >> 11;
                c4 = (quantizers[32] * coeffs[32]) >> 11;
                c5 = (quantizers[40] * coeffs[40]) >> 11;
                c6 = (quantizers[48] * coeffs[48]) >> 11;
                c7 = (quantizers[56] * coeffs[56]) >> 11;

                v13 = c4 + c0;
                v14 = c0 - c4;
                v16 = c6 + c2;
                v17 = (C2 * (c2 - c6) >> 11) - (c6 + c2);
                v60 = v14 - v17;
                v58 = v17 + v14;
                v56 = v16 + v13;
                v62 = v13 - v16;
                v52 = c5 + c3;
                v21 = c5 - c3;
                v22 = c1 + c7;
                v23 = c1 - c7;
                v66 = v22 + v52;
                v24 = C3 * (v23 + v21) >> 11;
                v25 = v24 + ((C4 * v21) >> 11) - (v22 + v52);
                v26 = (C2 * (v22 - v52) >> 11) - v25;
                v64 = v26 + (C1 * v23 >> 11) - v24;

                workspace[0] = v56 + v66;
                workspace[8] = v25 + v58;
                workspace[16] = v26 + v60;
                workspace[24] = v62 - v64;
                workspace[32] = v64 + v62;
                workspace[40] = v60 - v26;
                workspace[48] = v58 - v25;
                workspace[56] = v56 - v66;
            }
            else
            {
                int dc = (quantizers[0] * coeffs[0]) >> 11;
                workspace[0] = dc;
                workspace[8] = dc;
                workspace[16] = dc;
                workspace[24] = dc;
                workspace[32] = dc;
                workspace[40] = dc;
                workspace[48] = dc;
                workspace[56] = dc;
            }
            coeffs++;
            workspace++;
            quantizers = quantizers.Slice(1);
        }

        workspace = tmp;
        for (int i = 0; i < 8; i++)
        {
            
            c0 = workspace[0];
            c1 = workspace[1];
            c2 = workspace[2];
            c3 = workspace[3];
            c4 = workspace[4];
            c5 = workspace[5];
            c6 = workspace[6];
            c7 = workspace[7];

            v32 = c0 + c4;
            v61 = (c0 - c4);
            v34 = c2 + c6;
            v35 = v34 + v32;
            v36 = v32 - v34;
            v37 = (C2 * (c2 - c6) >> 11) - v34;
            v39 = v37 + v61;
            v61 = (v61 - v37);
            v43 = c3 + c5;
            v44 = c5 - c3;
            v45 = c1 + c7;
            v46 = c1 - c7;
            v67 = v45 + v43;
            v47 = C3 * (v46 + v44) >> 11;
            v48 = v47 + ((C4 * v44) >> 11) - (v45 + v43);
            v49 = (C2 * (v45 - v43) >> 11) - v48;
            v50 = v49 + (C1 * v46 >> 11) - v47;

            dst[0] = Clamp(v67 + v35);
            dst[1] = Clamp(v48 + v39);
            dst[2] = Clamp(v61 + v49);
            dst[3] = Clamp(v36 - v50);
            dst[4] = Clamp(v50 + v36);
            dst[5] = Clamp(v61 - v49);
            dst[6] = Clamp(v39 - v48);
            dst[7] = Clamp(v35 - v67);
            workspace += 8;
            dst += 8;
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short Clamp(int i) => unchecked((short)((ushort)(i + 127) >> 8));
}
