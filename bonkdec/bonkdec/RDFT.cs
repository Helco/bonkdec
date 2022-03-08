/*
 * Copyright Takuya OOURA, 1996-2001
 *
 * You may use, copy, modify and distribute this code
 * for any purpose (include commercial use) and without fee.
 * Please refer to this package when you modify this code.
 */
// The original code was retrieved from https://www.kurims.kyoto-u.ac.jp/~ooura/fft.html
// and subsequently ported to C# as well as modified to work with float instead of double
using System;

namespace Bonk;

/**
 * Fast Fourier/Cosine/Sine Transform.
 * <pre>
 *  dimension   :one
 *  data length :power of 2
 *  decimation  :frequency
 *  radix       :<b>split-radix</b>
 *  data        :inplace
 *  table       :use
 * </pre>
 * <h4>Appendix:</h4>
 * <p>
 *  The cos/sin table is recalculated when the larger table required.
 *  w[] and ip[] are compatible with all routines.
 * </p>
 * @author <a href="mailto:ooura@mmm.t.u-tokyo.ac.jp">Takuya OOURA</a>
 * @author <a href="mailto:vavivavi@yahoo.co.jp">Naohide Sano</a> (nsano)
 * @version 0.00 060127 nsano port to java version <br>
 */

internal static class RDFT
{
    private const int CDFT_RECURSIVE_N = 512;

    /**
     * Real Discrete Fourier Transform.
     * <pre>
     *  [definition]
     *      &lt;case1&gt; RDFT
     *          R[k] = sum_j = 0 &amp; &circ; (n - 1) a[j] * cos(2 * pi * j * k / n), 0 &lt;= k &lt;= n / 2
     *          I[k] = sum_j = 0 &amp; &circ; (n - 1) a[j] * sin(2 * pi * j * k / n), 0 &lt; k &lt; n / 2
     *      &lt;case2&gt; IRDFT (excluding scale)
     *          a[k] = (R[0] + R[n / 2] * cos(pi * k)) / 2 +
     *              sum_j = 1 &amp; &circ; (n / 2 - 1) R[j] * cos(2 * pi * j * k / n) +
     *              sum_j = 1 &amp; &circ; (n / 2 - 1) I[j] * sin(2 * pi * j * k / n), 0 &lt;= k &lt; n
     *  [usage]
     *      &lt;case1&gt;
     *          ip[0] = 0; // first time only
     *          rdft(n, 1, a, ip, w);
     *      &lt;case2&gt;
     *          ip[0] = 0; // first time only
     *          rdft(n, -1, a, ip, w);
     *  [remark]
     *      Inverse of
     *          rdft(n, 1, a, ip, w);
     *      is
     *          rdft(n, -1, a, ip, w);
     *          for (j = 0; j &lt;= n - 1; j++) {
     *              a[j] *= 2.0 / n;
     *          }
     *      .
     * </pre>
     * @param n data length <br>
     *  n &gt;= 2, n = power of 2
     * @param isgn
     * @param a [0...n-1] input/output data
     * <pre>
     *  &lt;case1&gt;
     *      output data
     *          a[2 * k] = R[k], 0 &lt;= k &lt; n / 2
     *          a[2 * k + 1] = I[k], 0 &lt; k &lt; n / 2
     *          a[1] = R[n/2]
     *  &lt;case2&gt;
     *      input data
     *          a[2 * j] = R[j], 0 &lt;= j &lt; n / 2
     *          a[2 * j + 1] = I[j], 0 &lt; j &lt; n / 2
     *          a[1] = R[n / 2]
     * </pre>
     * @param ip [0...*] work area for bit reversal
     * <pre>
     *  length of ip &gt;= 2 + sqrt(n / 2)
     *  strictly,
     *  length of ip &gt;=
     *      2 + (1 &lt;&lt; (int) (log(n / 2 + 0.5) / log(2)) / 2).
     * </pre>
     *  ip[0],ip[1] are pointers of the cos/sin table.
     * @param w [0...n/2-1] cos/sin table <br>
     *  w[],ip[] are initialized if ip[0] == 0.
     */
    public static void rdft(int n, int isgn, float[] a, int[] ip, float[] w)
    {
        int nw, nc;
        float xi;

        nw = ip[0];
        if (n > (nw << 2))
        {
            nw = n >> 2;
            makewt(nw, ip, w);
        }
        nc = ip[1];
        if (n > (nc << 2))
        {
            nc = n >> 2;
            makect(nc, ip, w, nw);
        }
        if (isgn >= 0)
        {
            if (n > 4)
            {
                cftfsub(n, a, ip, 2, nw, w);
                rftfsub(n, a, nc, w, nw);
            }
            else if (n == 4)
            {
                cftfsub(n, a, ip, 2, nw, w);
            }
            xi = a[0] - a[1];
            a[0] += a[1];
            a[1] = xi;
        }
        else
        {
            a[1] = 0.5f * (a[0] - a[1]);
            a[0] -= a[1];
            if (n > 4)
            {
                rftbsub(n, a, nc, w, nw);
                cftbsub(n, a, ip, 2, nw, w);
            }
            else if (n == 4)
            {
                cftbsub(n, a, ip, 2, nw, w);
            }
        }
    }

    private static void makewt(int nw, int[] ip, float[] w)
    {
        int j, nwh, nw0, nw1;
        float delta, wn4r, wk1r, wk1i, wk3r, wk3i;

        ip[0] = nw;
        ip[1] = 1;
        if (nw > 2)
        {
            nwh = nw >> 1;
            delta = MathF.PI / 4 / nwh;
            wn4r = MathF.Cos(delta * nwh);
            w[0] = 1;
            w[1] = wn4r;
            if (nwh >= 4)
            {
                w[2] = 0.5f / MathF.Cos(delta * 2);
                w[3] = 0.5f / MathF.Cos(delta * 6);
            }
            for (j = 4; j < nwh; j += 4)
            {
                w[j] = MathF.Cos(delta * j);
                w[j + 1] = MathF.Sin(delta * j);
                w[j + 2] = MathF.Cos(3 * delta * j);
                w[j + 3] = MathF.Sin(3 * delta * j);
            }
            nw0 = 0;
            while (nwh > 2)
            {
                nw1 = nw0 + nwh;
                nwh >>= 1;
                w[nw1] = 1;
                w[nw1 + 1] = wn4r;
                if (nwh >= 4)
                {
                    wk1r = w[nw0 + 4];
                    wk3r = w[nw0 + 6];
                    w[nw1 + 2] = 0.5f / wk1r;
                    w[nw1 + 3] = 0.5f / wk3r;
                }
                for (j = 4; j < nwh; j += 4)
                {
                    wk1r = w[nw0 + 2 * j];
                    wk1i = w[nw0 + 2 * j + 1];
                    wk3r = w[nw0 + 2 * j + 2];
                    wk3i = w[nw0 + 2 * j + 3];
                    w[nw1 + j] = wk1r;
                    w[nw1 + j + 1] = wk1i;
                    w[nw1 + j + 2] = wk3r;
                    w[nw1 + j + 3] = wk3i;
                }
                nw0 = nw1;
            }
        }
    }

    private static void makect(int nc, int[] ip, float[] c, int cP)
    {
        int j, nch;
        float delta;

        ip[1] = nc;
        if (nc > 1)
        {
            nch = nc >> 1;
            //          delta = Math.atan(1.0) / nch;
            delta = MathF.PI / 4 / nch;
            c[cP + 0] = MathF.Cos(delta * nch);
            c[cP + nch] = 0.5f * c[cP + 0];
            for (j = 1; j < nch; j++)
            {
                c[cP + j] = 0.5f * MathF.Cos(delta * j);
                c[cP + nc - j] = 0.5f * MathF.Sin(delta * j);
            }
        }
    }

    /**
     * 2nd
     * @see #rdft(int, int, float[], int[], float[])
     * @see #ddct(int, int, float[], int[], float[])
     * @see #cdft(int, int, float[], int[], float[])
     * @see #ddst(int, int, float[], int[], float[])
     * @see #dfst(int, float[], float[], int[], float[])
     * @see #dfct(int, float[], float[], int[], float[])
     */
    private static void cftfsub(int n, float[] a, int[] ip, int ipP, int nw, float[] w)
    {
        int m;

        if (n > 32)
        {
            m = n >> 2;
            cftf1st(n, a, w, nw - m);
            if (n > CDFT_RECURSIVE_N)
            {
                cftrec1(m, a, 0, nw, w);
                cftrec2(m, a, m, nw, w);
                cftrec1(m, a, 2 * m, nw, w);
                cftrec1(m, a, 3 * m, nw, w);
            }
            else if (m > 32)
            {
                cftexp1(n, a, 0, nw, w);
            }
            else
            {
                cftfx41(n, a, 0, nw, w);
            }
            bitrv2(n, ip, ipP, a);
        }
        else if (n > 8)
        {
            if (n == 32)
            {
                cftf161(a, 0, w, nw - 8);
                bitrv216(a);
            }
            else
            {
                cftf081(a, 0, w, 0);
                bitrv208(a);
            }
        }
        else if (n == 8)
        {
            cftf040(a);
        }
        else if (n == 4)
        {
            cftx020(a);
        }
    }

    /**
     * 2nd
     * @see #rdft(int, int, float[], int[], float[])
     * @see #ddct(int, int, float[], int[], float[])
     * @see #cdft(int, int, float[], int[], float[])
     * @see #ddst(int, int, float[], int[], float[])
     */
    private static void cftbsub(int n, float[] a, int[] ip, int ipP, int nw, float[] w)
    {
        int m;

        if (n > 32)
        {
            m = n >> 2;
            cftb1st(n, a, w, nw - m);
            if (n > CDFT_RECURSIVE_N)
            {
                cftrec1(m, a, 0, nw, w);
                cftrec2(m, a, m, nw, w);
                cftrec1(m, a, 2 * m, nw, w);
                cftrec1(m, a, 3 * m, nw, w);
            }
            else if (m > 32)
            {
                cftexp1(n, a, 0, nw, w);
            }
            else
            {
                cftfx41(n, a, 0, nw, w);
            }
            bitrv2conj(n, ip, ipP, a);
        }
        else if (n > 8)
        {
            if (n == 32)
            {
                cftf161(a, 0, w, nw - 8);
                bitrv216neg(a);
            }
            else
            {
                cftf081(a, 0, w, 0);
                bitrv208neg(a);
            }
        }
        else if (n == 8)
        {
            cftb040(a);
        }
        else if (n == 4)
        {
            cftx020(a);
        }
    }

    /**
     * 3rd
     * @see #cftfsub(int, float[], int[], int, int, float[])
     */
    private static void bitrv2(int n, int[] ip, int ipP, float[] a)
    {
        int j, j1, k, k1, l, m, m2;
        float xr, xi, yr, yi;

        ip[ipP + 0] = 0;
        l = n;
        m = 1;
        while ((m << 3) < l)
        {
            l >>= 1;
            for (j = 0; j < m; j++)
            {
                ip[ipP + m + j] = ip[ipP + j] + l;
            }
            m <<= 1;
        }
        m2 = 2 * m;
        if ((m << 3) == l)
        {
            for (k = 0; k < m; k++)
            {
                for (j = 0; j < k; j++)
                {
                    j1 = 2 * j + ip[ipP + k];
                    k1 = 2 * k + ip[ipP + j];
                    xr = a[j1];
                    xi = a[j1 + 1];
                    yr = a[k1];
                    yi = a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                    j1 += m2;
                    k1 += 2 * m2;
                    xr = a[j1];
                    xi = a[j1 + 1];
                    yr = a[k1];
                    yi = a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                    j1 += m2;
                    k1 -= m2;
                    xr = a[j1];
                    xi = a[j1 + 1];
                    yr = a[k1];
                    yi = a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                    j1 += m2;
                    k1 += 2 * m2;
                    xr = a[j1];
                    xi = a[j1 + 1];
                    yr = a[k1];
                    yi = a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                }
                j1 = 2 * k + m2 + ip[ipP + k];
                k1 = j1 + m2;
                xr = a[j1];
                xi = a[j1 + 1];
                yr = a[k1];
                yi = a[k1 + 1];
                a[j1] = yr;
                a[j1 + 1] = yi;
                a[k1] = xr;
                a[k1 + 1] = xi;
            }
        }
        else
        {
            for (k = 1; k < m; k++)
            {
                for (j = 0; j < k; j++)
                {
                    j1 = 2 * j + ip[ipP + k];
                    k1 = 2 * k + ip[ipP + j];
                    xr = a[j1];
                    xi = a[j1 + 1];
                    yr = a[k1];
                    yi = a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                    j1 += m2;
                    k1 += m2;
                    xr = a[j1];
                    xi = a[j1 + 1];
                    yr = a[k1];
                    yi = a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                }
            }
        }
    }

    /**
     * 3rd
     * @see #cftbsub(int, float[], int[], int, int, float[])
     */
    private static void bitrv2conj(int n, int[] ip, int ipP, float[] a)
    {
        int j, j1, k, k1, l, m, m2;
        float xr, xi, yr, yi;

        ip[ipP + 0] = 0;
        l = n;
        m = 1;
        while ((m << 3) < l)
        {
            l >>= 1;
            for (j = 0; j < m; j++)
            {
                ip[ipP + m + j] = ip[ipP + j] + l;
            }
            m <<= 1;
        }
        m2 = 2 * m;
        if ((m << 3) == l)
        {
            for (k = 0; k < m; k++)
            {
                for (j = 0; j < k; j++)
                {
                    j1 = 2 * j + ip[ipP + k];
                    k1 = 2 * k + ip[ipP + j];
                    xr = a[j1];
                    xi = -a[j1 + 1];
                    yr = a[k1];
                    yi = -a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                    j1 += m2;
                    k1 += 2 * m2;
                    xr = a[j1];
                    xi = -a[j1 + 1];
                    yr = a[k1];
                    yi = -a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                    j1 += m2;
                    k1 -= m2;
                    xr = a[j1];
                    xi = -a[j1 + 1];
                    yr = a[k1];
                    yi = -a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                    j1 += m2;
                    k1 += 2 * m2;
                    xr = a[j1];
                    xi = -a[j1 + 1];
                    yr = a[k1];
                    yi = -a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                }
                k1 = 2 * k + ip[ipP + k];
                a[k1 + 1] = -a[k1 + 1];
                j1 = k1 + m2;
                k1 = j1 + m2;
                xr = a[j1];
                xi = -a[j1 + 1];
                yr = a[k1];
                yi = -a[k1 + 1];
                a[j1] = yr;
                a[j1 + 1] = yi;
                a[k1] = xr;
                a[k1 + 1] = xi;
                k1 += m2;
                a[k1 + 1] = -a[k1 + 1];
            }
        }
        else
        {
            a[1] = -a[1];
            a[m2 + 1] = -a[m2 + 1];
            for (k = 1; k < m; k++)
            {
                for (j = 0; j < k; j++)
                {
                    j1 = 2 * j + ip[ipP + k];
                    k1 = 2 * k + ip[ipP + j];
                    xr = a[j1];
                    xi = -a[j1 + 1];
                    yr = a[k1];
                    yi = -a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                    j1 += m2;
                    k1 += m2;
                    xr = a[j1];
                    xi = -a[j1 + 1];
                    yr = a[k1];
                    yi = -a[k1 + 1];
                    a[j1] = yr;
                    a[j1 + 1] = yi;
                    a[k1] = xr;
                    a[k1 + 1] = xi;
                }
                k1 = 2 * k + ip[ipP + k];
                a[k1 + 1] = -a[k1 + 1];
                a[k1 + m2 + 1] = -a[k1 + m2 + 1];
            }
        }
    }

    /**
     * 3rd
     * @see #cftfsub(int, float[], int[], int, int, float[])
     */
    private static void bitrv216(float[] a)
    {
        float x1r, x1i, x2r, x2i, x3r, x3i, x4r, x4i, x5r, x5i, x7r, x7i, x8r, x8i, x10r, x10i, x11r, x11i, x12r, x12i, x13r, x13i, x14r, x14i;

        x1r = a[2];
        x1i = a[3];
        x2r = a[4];
        x2i = a[5];
        x3r = a[6];
        x3i = a[7];
        x4r = a[8];
        x4i = a[9];
        x5r = a[10];
        x5i = a[11];
        x7r = a[14];
        x7i = a[15];
        x8r = a[16];
        x8i = a[17];
        x10r = a[20];
        x10i = a[21];
        x11r = a[22];
        x11i = a[23];
        x12r = a[24];
        x12i = a[25];
        x13r = a[26];
        x13i = a[27];
        x14r = a[28];
        x14i = a[29];
        a[2] = x8r;
        a[3] = x8i;
        a[4] = x4r;
        a[5] = x4i;
        a[6] = x12r;
        a[7] = x12i;
        a[8] = x2r;
        a[9] = x2i;
        a[10] = x10r;
        a[11] = x10i;
        a[14] = x14r;
        a[15] = x14i;
        a[16] = x1r;
        a[17] = x1i;
        a[20] = x5r;
        a[21] = x5i;
        a[22] = x13r;
        a[23] = x13i;
        a[24] = x3r;
        a[25] = x3i;
        a[26] = x11r;
        a[27] = x11i;
        a[28] = x7r;
        a[29] = x7i;
    }

    /**
     * 3rd
     * @see #cftbsub(int, float[], int[], int, int, float[])
     */
    private static void bitrv216neg(float[] a)
    {
        float x1r, x1i, x2r, x2i, x3r, x3i, x4r, x4i, x5r, x5i, x6r, x6i, x7r, x7i, x8r, x8i, x9r, x9i, x10r, x10i, x11r, x11i, x12r, x12i, x13r, x13i, x14r, x14i, x15r, x15i;

        x1r = a[2];
        x1i = a[3];
        x2r = a[4];
        x2i = a[5];
        x3r = a[6];
        x3i = a[7];
        x4r = a[8];
        x4i = a[9];
        x5r = a[10];
        x5i = a[11];
        x6r = a[12];
        x6i = a[13];
        x7r = a[14];
        x7i = a[15];
        x8r = a[16];
        x8i = a[17];
        x9r = a[18];
        x9i = a[19];
        x10r = a[20];
        x10i = a[21];
        x11r = a[22];
        x11i = a[23];
        x12r = a[24];
        x12i = a[25];
        x13r = a[26];
        x13i = a[27];
        x14r = a[28];
        x14i = a[29];
        x15r = a[30];
        x15i = a[31];
        a[2] = x15r;
        a[3] = x15i;
        a[4] = x7r;
        a[5] = x7i;
        a[6] = x11r;
        a[7] = x11i;
        a[8] = x3r;
        a[9] = x3i;
        a[10] = x13r;
        a[11] = x13i;
        a[12] = x5r;
        a[13] = x5i;
        a[14] = x9r;
        a[15] = x9i;
        a[16] = x1r;
        a[17] = x1i;
        a[18] = x14r;
        a[19] = x14i;
        a[20] = x6r;
        a[21] = x6i;
        a[22] = x10r;
        a[23] = x10i;
        a[24] = x2r;
        a[25] = x2i;
        a[26] = x12r;
        a[27] = x12i;
        a[28] = x4r;
        a[29] = x4i;
        a[30] = x8r;
        a[31] = x8i;
    }

    /**
     * 3rd
     * @see #cftfsub(int, float[], int[], int, int, float[])
     */
    private static void bitrv208(float[] a)
    {
        float x1r, x1i, x3r, x3i, x4r, x4i, x6r, x6i;

        x1r = a[2];
        x1i = a[3];
        x3r = a[6];
        x3i = a[7];
        x4r = a[8];
        x4i = a[9];
        x6r = a[12];
        x6i = a[13];
        a[2] = x4r;
        a[3] = x4i;
        a[6] = x6r;
        a[7] = x6i;
        a[8] = x1r;
        a[9] = x1i;
        a[12] = x3r;
        a[13] = x3i;
    }

    /**
     * 3rd
     * @see #cftbsub(int, float[], int[], int, int, float[])
     */
    private static void bitrv208neg(float[] a)
    {
        float x1r, x1i, x2r, x2i, x3r, x3i, x4r, x4i, x5r, x5i, x6r, x6i, x7r, x7i;

        x1r = a[2];
        x1i = a[3];
        x2r = a[4];
        x2i = a[5];
        x3r = a[6];
        x3i = a[7];
        x4r = a[8];
        x4i = a[9];
        x5r = a[10];
        x5i = a[11];
        x6r = a[12];
        x6i = a[13];
        x7r = a[14];
        x7i = a[15];
        a[2] = x7r;
        a[3] = x7i;
        a[4] = x3r;
        a[5] = x3i;
        a[6] = x5r;
        a[7] = x5i;
        a[8] = x1r;
        a[9] = x1i;
        a[10] = x6r;
        a[11] = x6i;
        a[12] = x2r;
        a[13] = x2i;
        a[14] = x4r;
        a[15] = x4i;
    }

    /**
     * 3rd
     * @see #cftfsub(int, float[], int[], int, int, float[])
     */
    private static void cftf1st(int n, float[] a, float[] w, int wP)
    {
        int j, j0, j1, j2, j3, k, m, mh;
        float wn4r, csc1, csc3, wk1r, wk1i, wk3r, wk3i, wd1r, wd1i, wd3r, wd3i;
        float x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i, y0r, y0i, y1r, y1i, y2r, y2i, y3r, y3i;

        mh = n >> 3;
        m = 2 * mh;
        j1 = m;
        j2 = j1 + m;
        j3 = j2 + m;
        x0r = a[0] + a[j2];
        x0i = a[1] + a[j2 + 1];
        x1r = a[0] - a[j2];
        x1i = a[1] - a[j2 + 1];
        x2r = a[j1] + a[j3];
        x2i = a[j1 + 1] + a[j3 + 1];
        x3r = a[j1] - a[j3];
        x3i = a[j1 + 1] - a[j3 + 1];
        a[0] = x0r + x2r;
        a[1] = x0i + x2i;
        a[j1] = x0r - x2r;
        a[j1 + 1] = x0i - x2i;
        a[j2] = x1r - x3i;
        a[j2 + 1] = x1i + x3r;
        a[j3] = x1r + x3i;
        a[j3 + 1] = x1i - x3r;
        wn4r = w[wP + 1];
        csc1 = w[wP + 2];
        csc3 = w[wP + 3];
        wd1r = 1;
        wd1i = 0;
        wd3r = 1;
        wd3i = 0;
        k = 0;
        for (j = 2; j < mh - 2; j += 4)
        {
            k += 4;
            wk1r = csc1 * (wd1r + w[wP + k]);
            wk1i = csc1 * (wd1i + w[wP + k + 1]);
            wk3r = csc3 * (wd3r + w[wP + k + 2]);
            wk3i = csc3 * (wd3i - w[wP + k + 3]);
            wd1r = w[wP + k];
            wd1i = w[wP + k + 1];
            wd3r = w[wP + k + 2];
            wd3i = -w[wP + k + 3];
            j1 = j + m;
            j2 = j1 + m;
            j3 = j2 + m;
            x0r = a[j] + a[j2];
            x0i = a[j + 1] + a[j2 + 1];
            x1r = a[j] - a[j2];
            x1i = a[j + 1] - a[j2 + 1];
            y0r = a[j + 2] + a[j2 + 2];
            y0i = a[j + 3] + a[j2 + 3];
            y1r = a[j + 2] - a[j2 + 2];
            y1i = a[j + 3] - a[j2 + 3];
            x2r = a[j1] + a[j3];
            x2i = a[j1 + 1] + a[j3 + 1];
            x3r = a[j1] - a[j3];
            x3i = a[j1 + 1] - a[j3 + 1];
            y2r = a[j1 + 2] + a[j3 + 2];
            y2i = a[j1 + 3] + a[j3 + 3];
            y3r = a[j1 + 2] - a[j3 + 2];
            y3i = a[j1 + 3] - a[j3 + 3];
            a[j] = x0r + x2r;
            a[j + 1] = x0i + x2i;
            a[j + 2] = y0r + y2r;
            a[j + 3] = y0i + y2i;
            a[j1] = x0r - x2r;
            a[j1 + 1] = x0i - x2i;
            a[j1 + 2] = y0r - y2r;
            a[j1 + 3] = y0i - y2i;
            x0r = x1r - x3i;
            x0i = x1i + x3r;
            a[j2] = wk1r * x0r - wk1i * x0i;
            a[j2 + 1] = wk1r * x0i + wk1i * x0r;
            x0r = y1r - y3i;
            x0i = y1i + y3r;
            a[j2 + 2] = wd1r * x0r - wd1i * x0i;
            a[j2 + 3] = wd1r * x0i + wd1i * x0r;
            x0r = x1r + x3i;
            x0i = x1i - x3r;
            a[j3] = wk3r * x0r + wk3i * x0i;
            a[j3 + 1] = wk3r * x0i - wk3i * x0r;
            x0r = y1r + y3i;
            x0i = y1i - y3r;
            a[j3 + 2] = wd3r * x0r + wd3i * x0i;
            a[j3 + 3] = wd3r * x0i - wd3i * x0r;
            j0 = m - j;
            j1 = j0 + m;
            j2 = j1 + m;
            j3 = j2 + m;
            x0r = a[j0] + a[j2];
            x0i = a[j0 + 1] + a[j2 + 1];
            x1r = a[j0] - a[j2];
            x1i = a[j0 + 1] - a[j2 + 1];
            y0r = a[j0 - 2] + a[j2 - 2];
            y0i = a[j0 - 1] + a[j2 - 1];
            y1r = a[j0 - 2] - a[j2 - 2];
            y1i = a[j0 - 1] - a[j2 - 1];
            x2r = a[j1] + a[j3];
            x2i = a[j1 + 1] + a[j3 + 1];
            x3r = a[j1] - a[j3];
            x3i = a[j1 + 1] - a[j3 + 1];
            y2r = a[j1 - 2] + a[j3 - 2];
            y2i = a[j1 - 1] + a[j3 - 1];
            y3r = a[j1 - 2] - a[j3 - 2];
            y3i = a[j1 - 1] - a[j3 - 1];
            a[j0] = x0r + x2r;
            a[j0 + 1] = x0i + x2i;
            a[j0 - 2] = y0r + y2r;
            a[j0 - 1] = y0i + y2i;
            a[j1] = x0r - x2r;
            a[j1 + 1] = x0i - x2i;
            a[j1 - 2] = y0r - y2r;
            a[j1 - 1] = y0i - y2i;
            x0r = x1r - x3i;
            x0i = x1i + x3r;
            a[j2] = wk1i * x0r - wk1r * x0i;
            a[j2 + 1] = wk1i * x0i + wk1r * x0r;
            x0r = y1r - y3i;
            x0i = y1i + y3r;
            a[j2 - 2] = wd1i * x0r - wd1r * x0i;
            a[j2 - 1] = wd1i * x0i + wd1r * x0r;
            x0r = x1r + x3i;
            x0i = x1i - x3r;
            a[j3] = wk3i * x0r + wk3r * x0i;
            a[j3 + 1] = wk3i * x0i - wk3r * x0r;
            x0r = y1r + y3i;
            x0i = y1i - y3r;
            a[j3 - 2] = wd3i * x0r + wd3r * x0i;
            a[j3 - 1] = wd3i * x0i - wd3r * x0r;
        }
        wk1r = csc1 * (wd1r + wn4r);
        wk1i = csc1 * (wd1i + wn4r);
        wk3r = csc3 * (wd3r - wn4r);
        wk3i = csc3 * (wd3i - wn4r);
        j0 = mh;
        j1 = j0 + m;
        j2 = j1 + m;
        j3 = j2 + m;
        x0r = a[j0 - 2] + a[j2 - 2];
        x0i = a[j0 - 1] + a[j2 - 1];
        x1r = a[j0 - 2] - a[j2 - 2];
        x1i = a[j0 - 1] - a[j2 - 1];
        x2r = a[j1 - 2] + a[j3 - 2];
        x2i = a[j1 - 1] + a[j3 - 1];
        x3r = a[j1 - 2] - a[j3 - 2];
        x3i = a[j1 - 1] - a[j3 - 1];
        a[j0 - 2] = x0r + x2r;
        a[j0 - 1] = x0i + x2i;
        a[j1 - 2] = x0r - x2r;
        a[j1 - 1] = x0i - x2i;
        x0r = x1r - x3i;
        x0i = x1i + x3r;
        a[j2 - 2] = wk1r * x0r - wk1i * x0i;
        a[j2 - 1] = wk1r * x0i + wk1i * x0r;
        x0r = x1r + x3i;
        x0i = x1i - x3r;
        a[j3 - 2] = wk3r * x0r + wk3i * x0i;
        a[j3 - 1] = wk3r * x0i - wk3i * x0r;
        x0r = a[j0] + a[j2];
        x0i = a[j0 + 1] + a[j2 + 1];
        x1r = a[j0] - a[j2];
        x1i = a[j0 + 1] - a[j2 + 1];
        x2r = a[j1] + a[j3];
        x2i = a[j1 + 1] + a[j3 + 1];
        x3r = a[j1] - a[j3];
        x3i = a[j1 + 1] - a[j3 + 1];
        a[j0] = x0r + x2r;
        a[j0 + 1] = x0i + x2i;
        a[j1] = x0r - x2r;
        a[j1 + 1] = x0i - x2i;
        x0r = x1r - x3i;
        x0i = x1i + x3r;
        a[j2] = wn4r * (x0r - x0i);
        a[j2 + 1] = wn4r * (x0i + x0r);
        x0r = x1r + x3i;
        x0i = x1i - x3r;
        a[j3] = -wn4r * (x0r + x0i);
        a[j3 + 1] = -wn4r * (x0i - x0r);
        x0r = a[j0 + 2] + a[j2 + 2];
        x0i = a[j0 + 3] + a[j2 + 3];
        x1r = a[j0 + 2] - a[j2 + 2];
        x1i = a[j0 + 3] - a[j2 + 3];
        x2r = a[j1 + 2] + a[j3 + 2];
        x2i = a[j1 + 3] + a[j3 + 3];
        x3r = a[j1 + 2] - a[j3 + 2];
        x3i = a[j1 + 3] - a[j3 + 3];
        a[j0 + 2] = x0r + x2r;
        a[j0 + 3] = x0i + x2i;
        a[j1 + 2] = x0r - x2r;
        a[j1 + 3] = x0i - x2i;
        x0r = x1r - x3i;
        x0i = x1i + x3r;
        a[j2 + 2] = wk1i * x0r - wk1r * x0i;
        a[j2 + 3] = wk1i * x0i + wk1r * x0r;
        x0r = x1r + x3i;
        x0i = x1i - x3r;
        a[j3 + 2] = wk3i * x0r + wk3r * x0i;
        a[j3 + 3] = wk3i * x0i - wk3r * x0r;
    }

    /**
     * 3rd
     * @see #cftbsub(int, float[], int[], int, int, float[])
     */
    private static void cftb1st(int n, float[] a, float[] w, int wP)
    {
        int j, j0, j1, j2, j3, k, m, mh;
        float wn4r, csc1, csc3, wk1r, wk1i, wk3r, wk3i, wd1r, wd1i, wd3r, wd3i;
        float x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i, y0r, y0i, y1r, y1i, y2r, y2i, y3r, y3i;

        mh = n >> 3;
        m = 2 * mh;
        j1 = m;
        j2 = j1 + m;
        j3 = j2 + m;
        x0r = a[0] + a[j2];
        x0i = -a[1] - a[j2 + 1];
        x1r = a[0] - a[j2];
        x1i = -a[1] + a[j2 + 1];
        x2r = a[j1] + a[j3];
        x2i = a[j1 + 1] + a[j3 + 1];
        x3r = a[j1] - a[j3];
        x3i = a[j1 + 1] - a[j3 + 1];
        a[0] = x0r + x2r;
        a[1] = x0i - x2i;
        a[j1] = x0r - x2r;
        a[j1 + 1] = x0i + x2i;
        a[j2] = x1r + x3i;
        a[j2 + 1] = x1i + x3r;
        a[j3] = x1r - x3i;
        a[j3 + 1] = x1i - x3r;
        wn4r = w[wP + 1];
        csc1 = w[wP + 2];
        csc3 = w[wP + 3];
        wd1r = 1;
        wd1i = 0;
        wd3r = 1;
        wd3i = 0;
        k = 0;
        for (j = 2; j < mh - 2; j += 4)
        {
            k += 4;
            wk1r = csc1 * (wd1r + w[wP + k]);
            wk1i = csc1 * (wd1i + w[wP + k + 1]);
            wk3r = csc3 * (wd3r + w[wP + k + 2]);
            wk3i = csc3 * (wd3i - w[wP + k + 3]);
            wd1r = w[wP + k];
            wd1i = w[wP + k + 1];
            wd3r = w[wP + k + 2];
            wd3i = -w[wP + k + 3];
            j1 = j + m;
            j2 = j1 + m;
            j3 = j2 + m;
            x0r = a[j] + a[j2];
            x0i = -a[j + 1] - a[j2 + 1];
            x1r = a[j] - a[j2];
            x1i = -a[j + 1] + a[j2 + 1];
            y0r = a[j + 2] + a[j2 + 2];
            y0i = -a[j + 3] - a[j2 + 3];
            y1r = a[j + 2] - a[j2 + 2];
            y1i = -a[j + 3] + a[j2 + 3];
            x2r = a[j1] + a[j3];
            x2i = a[j1 + 1] + a[j3 + 1];
            x3r = a[j1] - a[j3];
            x3i = a[j1 + 1] - a[j3 + 1];
            y2r = a[j1 + 2] + a[j3 + 2];
            y2i = a[j1 + 3] + a[j3 + 3];
            y3r = a[j1 + 2] - a[j3 + 2];
            y3i = a[j1 + 3] - a[j3 + 3];
            a[j] = x0r + x2r;
            a[j + 1] = x0i - x2i;
            a[j + 2] = y0r + y2r;
            a[j + 3] = y0i - y2i;
            a[j1] = x0r - x2r;
            a[j1 + 1] = x0i + x2i;
            a[j1 + 2] = y0r - y2r;
            a[j1 + 3] = y0i + y2i;
            x0r = x1r + x3i;
            x0i = x1i + x3r;
            a[j2] = wk1r * x0r - wk1i * x0i;
            a[j2 + 1] = wk1r * x0i + wk1i * x0r;
            x0r = y1r + y3i;
            x0i = y1i + y3r;
            a[j2 + 2] = wd1r * x0r - wd1i * x0i;
            a[j2 + 3] = wd1r * x0i + wd1i * x0r;
            x0r = x1r - x3i;
            x0i = x1i - x3r;
            a[j3] = wk3r * x0r + wk3i * x0i;
            a[j3 + 1] = wk3r * x0i - wk3i * x0r;
            x0r = y1r - y3i;
            x0i = y1i - y3r;
            a[j3 + 2] = wd3r * x0r + wd3i * x0i;
            a[j3 + 3] = wd3r * x0i - wd3i * x0r;
            j0 = m - j;
            j1 = j0 + m;
            j2 = j1 + m;
            j3 = j2 + m;
            x0r = a[j0] + a[j2];
            x0i = -a[j0 + 1] - a[j2 + 1];
            x1r = a[j0] - a[j2];
            x1i = -a[j0 + 1] + a[j2 + 1];
            y0r = a[j0 - 2] + a[j2 - 2];
            y0i = -a[j0 - 1] - a[j2 - 1];
            y1r = a[j0 - 2] - a[j2 - 2];
            y1i = -a[j0 - 1] + a[j2 - 1];
            x2r = a[j1] + a[j3];
            x2i = a[j1 + 1] + a[j3 + 1];
            x3r = a[j1] - a[j3];
            x3i = a[j1 + 1] - a[j3 + 1];
            y2r = a[j1 - 2] + a[j3 - 2];
            y2i = a[j1 - 1] + a[j3 - 1];
            y3r = a[j1 - 2] - a[j3 - 2];
            y3i = a[j1 - 1] - a[j3 - 1];
            a[j0] = x0r + x2r;
            a[j0 + 1] = x0i - x2i;
            a[j0 - 2] = y0r + y2r;
            a[j0 - 1] = y0i - y2i;
            a[j1] = x0r - x2r;
            a[j1 + 1] = x0i + x2i;
            a[j1 - 2] = y0r - y2r;
            a[j1 - 1] = y0i + y2i;
            x0r = x1r + x3i;
            x0i = x1i + x3r;
            a[j2] = wk1i * x0r - wk1r * x0i;
            a[j2 + 1] = wk1i * x0i + wk1r * x0r;
            x0r = y1r + y3i;
            x0i = y1i + y3r;
            a[j2 - 2] = wd1i * x0r - wd1r * x0i;
            a[j2 - 1] = wd1i * x0i + wd1r * x0r;
            x0r = x1r - x3i;
            x0i = x1i - x3r;
            a[j3] = wk3i * x0r + wk3r * x0i;
            a[j3 + 1] = wk3i * x0i - wk3r * x0r;
            x0r = y1r - y3i;
            x0i = y1i - y3r;
            a[j3 - 2] = wd3i * x0r + wd3r * x0i;
            a[j3 - 1] = wd3i * x0i - wd3r * x0r;
        }
        wk1r = csc1 * (wd1r + wn4r);
        wk1i = csc1 * (wd1i + wn4r);
        wk3r = csc3 * (wd3r - wn4r);
        wk3i = csc3 * (wd3i - wn4r);
        j0 = mh;
        j1 = j0 + m;
        j2 = j1 + m;
        j3 = j2 + m;
        x0r = a[j0 - 2] + a[j2 - 2];
        x0i = -a[j0 - 1] - a[j2 - 1];
        x1r = a[j0 - 2] - a[j2 - 2];
        x1i = -a[j0 - 1] + a[j2 - 1];
        x2r = a[j1 - 2] + a[j3 - 2];
        x2i = a[j1 - 1] + a[j3 - 1];
        x3r = a[j1 - 2] - a[j3 - 2];
        x3i = a[j1 - 1] - a[j3 - 1];
        a[j0 - 2] = x0r + x2r;
        a[j0 - 1] = x0i - x2i;
        a[j1 - 2] = x0r - x2r;
        a[j1 - 1] = x0i + x2i;
        x0r = x1r + x3i;
        x0i = x1i + x3r;
        a[j2 - 2] = wk1r * x0r - wk1i * x0i;
        a[j2 - 1] = wk1r * x0i + wk1i * x0r;
        x0r = x1r - x3i;
        x0i = x1i - x3r;
        a[j3 - 2] = wk3r * x0r + wk3i * x0i;
        a[j3 - 1] = wk3r * x0i - wk3i * x0r;
        x0r = a[j0] + a[j2];
        x0i = -a[j0 + 1] - a[j2 + 1];
        x1r = a[j0] - a[j2];
        x1i = -a[j0 + 1] + a[j2 + 1];
        x2r = a[j1] + a[j3];
        x2i = a[j1 + 1] + a[j3 + 1];
        x3r = a[j1] - a[j3];
        x3i = a[j1 + 1] - a[j3 + 1];
        a[j0] = x0r + x2r;
        a[j0 + 1] = x0i - x2i;
        a[j1] = x0r - x2r;
        a[j1 + 1] = x0i + x2i;
        x0r = x1r + x3i;
        x0i = x1i + x3r;
        a[j2] = wn4r * (x0r - x0i);
        a[j2 + 1] = wn4r * (x0i + x0r);
        x0r = x1r - x3i;
        x0i = x1i - x3r;
        a[j3] = -wn4r * (x0r + x0i);
        a[j3 + 1] = -wn4r * (x0i - x0r);
        x0r = a[j0 + 2] + a[j2 + 2];
        x0i = -a[j0 + 3] - a[j2 + 3];
        x1r = a[j0 + 2] - a[j2 + 2];
        x1i = -a[j0 + 3] + a[j2 + 3];
        x2r = a[j1 + 2] + a[j3 + 2];
        x2i = a[j1 + 3] + a[j3 + 3];
        x3r = a[j1 + 2] - a[j3 + 2];
        x3i = a[j1 + 3] - a[j3 + 3];
        a[j0 + 2] = x0r + x2r;
        a[j0 + 3] = x0i - x2i;
        a[j1 + 2] = x0r - x2r;
        a[j1 + 3] = x0i + x2i;
        x0r = x1r + x3i;
        x0i = x1i + x3r;
        a[j2 + 2] = wk1i * x0r - wk1r * x0i;
        a[j2 + 3] = wk1i * x0i + wk1r * x0r;
        x0r = x1r - x3i;
        x0i = x1i - x3r;
        a[j3 + 2] = wk3i * x0r + wk3r * x0i;
        a[j3 + 3] = wk3i * x0i - wk3r * x0r;
    }

    /** */
    private static void cftrec1(int n, float[] a, int aP, int nw, float[] w)
    {
        int m;

        m = n >> 2;
        cftmdl1(n, a, aP, w, nw - 2 * m);
        if (n > CDFT_RECURSIVE_N)
        {
            cftrec1(m, a, aP, nw, w);
            cftrec2(m, a, aP + m, nw, w);
            cftrec1(m, a, aP + 2 * m, nw, w);
            cftrec1(m, a, aP + 3 * m, nw, w);
        }
        else
        {
            cftexp1(n, a, aP, nw, w);
        }
    }

    /** */
    private static void cftrec2(int n, float[] a, int aP, int nw, float[] w)
    {
        int m;

        m = n >> 2;
        cftmdl2(n, a, aP, w, nw - n);
        if (n > CDFT_RECURSIVE_N)
        {
            cftrec1(m, a, aP, nw, w);
            cftrec2(m, a, aP + m, nw, w);
            cftrec1(m, a, aP + 2 * m, nw, w);
            cftrec2(m, a, aP + 3 * m, nw, w);
        }
        else
        {
            cftexp2(n, a, aP, nw, w);
        }
    }

    /** */
    private static void cftexp1(int n, float[] a, int aP, int nw, float[] w)
    {
        int j, k, l;

        l = n >> 2;
        while (l > 128)
        {
            for (k = l; k < n; k <<= 2)
            {
                for (j = k - l; j < n; j += 4 * k)
                {
                    cftmdl1(l, a, aP + j, w, nw - (l >> 1));
                    cftmdl2(l, a, aP + k + j, w, nw - l);
                    cftmdl1(l, a, aP + 2 * k + j, w, nw - (l >> 1));
                }
            }
            cftmdl1(l, a, aP + n - l, w, nw - (l >> 1));
            l >>= 2;
        }
        for (k = l; k < n; k <<= 2)
        {
            for (j = k - l; j < n; j += 4 * k)
            {
                cftmdl1(l, a, aP + j, w, nw - (l >> 1));
                cftfx41(l, a, aP + j, nw, w);
                cftmdl2(l, a, aP + k + j, w, nw - l);
                cftfx42(l, a, aP + k + j, nw, w);
                cftmdl1(l, a, aP + 2 * k + j, w, nw - (l >> 1));
                cftfx41(l, a, aP + 2 * k + j, nw, w);
            }
        }
        cftmdl1(l, a, aP + n - l, w, nw - (l >> 1));
        cftfx41(l, a, aP + n - l, nw, w);
    }

    /** */
    private static void cftexp2(int n, float[] a, int aP, int nw, float[] w)
    {
        int j, k, l, m;

        m = n >> 1;
        l = n >> 2;
        while (l > 128)
        {
            for (k = l; k < m; k <<= 2)
            {
                for (j = k - l; j < m; j += 2 * k)
                {
                    cftmdl1(l, a, aP + j, w, nw - (l >> 1));
                    cftmdl1(l, a, aP + m + j, w, nw - (l >> 1));
                }
                for (j = 2 * k - l; j < m; j += 4 * k)
                {
                    cftmdl2(l, a, aP + j, w, nw - l);
                    cftmdl2(l, a, aP + m + j, w, nw - l);
                }
            }
            l >>= 2;
        }
        for (k = l; k < m; k <<= 2)
        {
            for (j = k - l; j < m; j += 2 * k)
            {
                cftmdl1(l, a, aP + j, w, nw - (l >> 1));
                cftfx41(l, a, aP + j, nw, w);
                cftmdl1(l, a, aP + m + j, w, nw - (l >> 1));
                cftfx41(l, a, aP + m + j, nw, w);
            }
            for (j = 2 * k - l; j < m; j += 4 * k)
            {
                cftmdl2(l, a, aP + j, w, nw - l);
                cftfx42(l, a, aP + j, nw, w);
                cftmdl2(l, a, aP + m + j, w, nw - l);
                cftfx42(l, a, aP + m + j, nw, w);
            }
        }
    }

    /** */
    private static void cftmdl1(int n, float[] a, int aP, float[] w, int wP)
    {
        int j, j0, j1, j2, j3, k, m, mh;
        float wn4r, wk1r, wk1i, wk3r, wk3i;
        float x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i;

        mh = n >> 3;
        m = 2 * mh;
        j1 = m;
        j2 = j1 + m;
        j3 = j2 + m;
        x0r = a[aP + 0] + a[aP + j2];
        x0i = a[aP + 1] + a[aP + j2 + 1];
        x1r = a[aP + 0] - a[aP + j2];
        x1i = a[aP + 1] - a[aP + j2 + 1];
        x2r = a[aP + j1] + a[aP + j3];
        x2i = a[aP + j1 + 1] + a[aP + j3 + 1];
        x3r = a[aP + j1] - a[aP + j3];
        x3i = a[aP + j1 + 1] - a[aP + j3 + 1];
        a[aP + 0] = x0r + x2r;
        a[aP + 1] = x0i + x2i;
        a[aP + j1] = x0r - x2r;
        a[aP + j1 + 1] = x0i - x2i;
        a[aP + j2] = x1r - x3i;
        a[aP + j2 + 1] = x1i + x3r;
        a[aP + j3] = x1r + x3i;
        a[aP + j3 + 1] = x1i - x3r;
        wn4r = w[wP + 1];
        k = 0;
        for (j = 2; j < mh; j += 2)
        {
            k += 4;
            wk1r = w[wP + k];
            wk1i = w[wP + k + 1];
            wk3r = w[wP + k + 2];
            wk3i = -w[wP + k + 3];
            j1 = j + m;
            j2 = j1 + m;
            j3 = j2 + m;
            x0r = a[aP + j] + a[aP + j2];
            x0i = a[aP + j + 1] + a[aP + j2 + 1];
            x1r = a[aP + j] - a[aP + j2];
            x1i = a[aP + j + 1] - a[aP + j2 + 1];
            x2r = a[aP + j1] + a[aP + j3];
            x2i = a[aP + j1 + 1] + a[aP + j3 + 1];
            x3r = a[aP + j1] - a[aP + j3];
            x3i = a[aP + j1 + 1] - a[aP + j3 + 1];
            a[aP + j] = x0r + x2r;
            a[aP + j + 1] = x0i + x2i;
            a[aP + j1] = x0r - x2r;
            a[aP + j1 + 1] = x0i - x2i;
            x0r = x1r - x3i;
            x0i = x1i + x3r;
            a[aP + j2] = wk1r * x0r - wk1i * x0i;
            a[aP + j2 + 1] = wk1r * x0i + wk1i * x0r;
            x0r = x1r + x3i;
            x0i = x1i - x3r;
            a[aP + j3] = wk3r * x0r + wk3i * x0i;
            a[aP + j3 + 1] = wk3r * x0i - wk3i * x0r;
            j0 = m - j;
            j1 = j0 + m;
            j2 = j1 + m;
            j3 = j2 + m;
            x0r = a[aP + j0] + a[aP + j2];
            x0i = a[aP + j0 + 1] + a[aP + j2 + 1];
            x1r = a[aP + j0] - a[aP + j2];
            x1i = a[aP + j0 + 1] - a[aP + j2 + 1];
            x2r = a[aP + j1] + a[aP + j3];
            x2i = a[aP + j1 + 1] + a[aP + j3 + 1];
            x3r = a[aP + j1] - a[aP + j3];
            x3i = a[aP + j1 + 1] - a[aP + j3 + 1];
            a[aP + j0] = x0r + x2r;
            a[aP + j0 + 1] = x0i + x2i;
            a[aP + j1] = x0r - x2r;
            a[aP + j1 + 1] = x0i - x2i;
            x0r = x1r - x3i;
            x0i = x1i + x3r;
            a[aP + j2] = wk1i * x0r - wk1r * x0i;
            a[aP + j2 + 1] = wk1i * x0i + wk1r * x0r;
            x0r = x1r + x3i;
            x0i = x1i - x3r;
            a[aP + j3] = wk3i * x0r + wk3r * x0i;
            a[aP + j3 + 1] = wk3i * x0i - wk3r * x0r;
        }
        j0 = mh;
        j1 = j0 + m;
        j2 = j1 + m;
        j3 = j2 + m;
        x0r = a[aP + j0] + a[aP + j2];
        x0i = a[aP + j0 + 1] + a[aP + j2 + 1];
        x1r = a[aP + j0] - a[aP + j2];
        x1i = a[aP + j0 + 1] - a[aP + j2 + 1];
        x2r = a[aP + j1] + a[aP + j3];
        x2i = a[aP + j1 + 1] + a[aP + j3 + 1];
        x3r = a[aP + j1] - a[aP + j3];
        x3i = a[aP + j1 + 1] - a[aP + j3 + 1];
        a[aP + j0] = x0r + x2r;
        a[aP + j0 + 1] = x0i + x2i;
        a[aP + j1] = x0r - x2r;
        a[aP + j1 + 1] = x0i - x2i;
        x0r = x1r - x3i;
        x0i = x1i + x3r;
        a[aP + j2] = wn4r * (x0r - x0i);
        a[aP + j2 + 1] = wn4r * (x0i + x0r);
        x0r = x1r + x3i;
        x0i = x1i - x3r;
        a[aP + j3] = -wn4r * (x0r + x0i);
        a[aP + j3 + 1] = -wn4r * (x0i - x0r);
    }

    /** */
    private static void cftmdl2(int n, float[] a, int aP, float[] w, int wP)
    {
        int j, j0, j1, j2, j3, k, kr, m, mh;
        float wn4r, wk1r, wk1i, wk3r, wk3i, wd1r, wd1i, wd3r, wd3i;
        float x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i, y0r, y0i, y2r, y2i;

        mh = n >> 3;
        m = 2 * mh;
        wn4r = w[wP + 1];
        j1 = m;
        j2 = j1 + m;
        j3 = j2 + m;
        x0r = a[aP + 0] - a[aP + j2 + 1];
        x0i = a[aP + 1] + a[aP + j2];
        x1r = a[aP + 0] + a[aP + j2 + 1];
        x1i = a[aP + 1] - a[aP + j2];
        x2r = a[aP + j1] - a[aP + j3 + 1];
        x2i = a[aP + j1 + 1] + a[aP + j3];
        x3r = a[aP + j1] + a[aP + j3 + 1];
        x3i = a[aP + j1 + 1] - a[aP + j3];
        y0r = wn4r * (x2r - x2i);
        y0i = wn4r * (x2i + x2r);
        a[aP + 0] = x0r + y0r;
        a[aP + 1] = x0i + y0i;
        a[aP + j1] = x0r - y0r;
        a[aP + j1 + 1] = x0i - y0i;
        y0r = wn4r * (x3r - x3i);
        y0i = wn4r * (x3i + x3r);
        a[aP + j2] = x1r - y0i;
        a[aP + j2 + 1] = x1i + y0r;
        a[aP + j3] = x1r + y0i;
        a[aP + j3 + 1] = x1i - y0r;
        k = 0;
        kr = 2 * m;
        for (j = 2; j < mh; j += 2)
        {
            k += 4;
            wk1r = w[wP + k];
            wk1i = w[wP + k + 1];
            wk3r = w[wP + k + 2];
            wk3i = -w[wP + k + 3];
            kr -= 4;
            wd1i = w[wP + kr];
            wd1r = w[wP + kr + 1];
            wd3i = w[wP + kr + 2];
            wd3r = -w[wP + kr + 3];
            j1 = j + m;
            j2 = j1 + m;
            j3 = j2 + m;
            x0r = a[aP + j] - a[aP + j2 + 1];
            x0i = a[aP + j + 1] + a[aP + j2];
            x1r = a[aP + j] + a[aP + j2 + 1];
            x1i = a[aP + j + 1] - a[aP + j2];
            x2r = a[aP + j1] - a[aP + j3 + 1];
            x2i = a[aP + j1 + 1] + a[aP + j3];
            x3r = a[aP + j1] + a[aP + j3 + 1];
            x3i = a[aP + j1 + 1] - a[aP + j3];
            y0r = wk1r * x0r - wk1i * x0i;
            y0i = wk1r * x0i + wk1i * x0r;
            y2r = wd1r * x2r - wd1i * x2i;
            y2i = wd1r * x2i + wd1i * x2r;
            a[aP + j] = y0r + y2r;
            a[aP + j + 1] = y0i + y2i;
            a[aP + j1] = y0r - y2r;
            a[aP + j1 + 1] = y0i - y2i;
            y0r = wk3r * x1r + wk3i * x1i;
            y0i = wk3r * x1i - wk3i * x1r;
            y2r = wd3r * x3r + wd3i * x3i;
            y2i = wd3r * x3i - wd3i * x3r;
            a[aP + j2] = y0r + y2r;
            a[aP + j2 + 1] = y0i + y2i;
            a[aP + j3] = y0r - y2r;
            a[aP + j3 + 1] = y0i - y2i;
            j0 = m - j;
            j1 = j0 + m;
            j2 = j1 + m;
            j3 = j2 + m;
            x0r = a[aP + j0] - a[aP + j2 + 1];
            x0i = a[aP + j0 + 1] + a[aP + j2];
            x1r = a[aP + j0] + a[aP + j2 + 1];
            x1i = a[aP + j0 + 1] - a[aP + j2];
            x2r = a[aP + j1] - a[aP + j3 + 1];
            x2i = a[aP + j1 + 1] + a[aP + j3];
            x3r = a[aP + j1] + a[aP + j3 + 1];
            x3i = a[aP + j1 + 1] - a[aP + j3];
            y0r = wd1i * x0r - wd1r * x0i;
            y0i = wd1i * x0i + wd1r * x0r;
            y2r = wk1i * x2r - wk1r * x2i;
            y2i = wk1i * x2i + wk1r * x2r;
            a[aP + j0] = y0r + y2r;
            a[aP + j0 + 1] = y0i + y2i;
            a[aP + j1] = y0r - y2r;
            a[aP + j1 + 1] = y0i - y2i;
            y0r = wd3i * x1r + wd3r * x1i;
            y0i = wd3i * x1i - wd3r * x1r;
            y2r = wk3i * x3r + wk3r * x3i;
            y2i = wk3i * x3i - wk3r * x3r;
            a[aP + j2] = y0r + y2r;
            a[aP + j2 + 1] = y0i + y2i;
            a[aP + j3] = y0r - y2r;
            a[aP + j3 + 1] = y0i - y2i;
        }
        wk1r = w[wP + m];
        wk1i = w[wP + m + 1];
        j0 = mh;
        j1 = j0 + m;
        j2 = j1 + m;
        j3 = j2 + m;
        x0r = a[aP + j0] - a[aP + j2 + 1];
        x0i = a[aP + j0 + 1] + a[aP + j2];
        x1r = a[aP + j0] + a[aP + j2 + 1];
        x1i = a[aP + j0 + 1] - a[aP + j2];
        x2r = a[aP + j1] - a[aP + j3 + 1];
        x2i = a[aP + j1 + 1] + a[aP + j3];
        x3r = a[aP + j1] + a[aP + j3 + 1];
        x3i = a[aP + j1 + 1] - a[aP + j3];
        y0r = wk1r * x0r - wk1i * x0i;
        y0i = wk1r * x0i + wk1i * x0r;
        y2r = wk1i * x2r - wk1r * x2i;
        y2i = wk1i * x2i + wk1r * x2r;
        a[aP + j0] = y0r + y2r;
        a[aP + j0 + 1] = y0i + y2i;
        a[aP + j1] = y0r - y2r;
        a[aP + j1 + 1] = y0i - y2i;
        y0r = wk1i * x1r - wk1r * x1i;
        y0i = wk1i * x1i + wk1r * x1r;
        y2r = wk1r * x3r - wk1i * x3i;
        y2i = wk1r * x3i + wk1i * x3r;
        a[aP + j2] = y0r - y2r;
        a[aP + j2 + 1] = y0i - y2i;
        a[aP + j3] = y0r + y2r;
        a[aP + j3 + 1] = y0i + y2i;
    }

    /** */
    private static void cftfx41(int n, float[] a, int aP, int nw, float[] w)
    {
        if (n == 128)
        {
            cftf161(a, aP, w, nw - 8);
            cftf162(a, aP + 32, w, nw - 32);
            cftf161(a, aP + 64, w, nw - 8);
            cftf161(a, aP + 96, w, nw - 8);
        }
        else
        {
            cftf081(a, aP, w, nw - 16);
            cftf082(a, aP + 16, w, nw - 16);
            cftf081(a, aP + 32, w, nw - 16);
            cftf081(a, aP + 48, w, nw - 16);
        }
    }

    /** */
    private static void cftfx42(int n, float[] a, int aP, int nw, float[] w)
    {
        if (n == 128)
        {
            cftf161(a, aP, w, nw - 8);
            cftf162(a, aP + 32, w, nw - 32);
            cftf161(a, aP + 64, w, nw - 8);
            cftf162(a, aP + 96, w, nw - 32);
        }
        else
        {
            cftf081(a, aP, w, nw - 16);
            cftf082(a, aP + 16, w, nw - 16);
            cftf081(a, aP + 32, w, nw - 16);
            cftf082(a, aP + 48, w, nw - 16);
        }
    }

    /** */
    private static void cftf161(float[] a, int aP, float[] w, int wP)
    {
        float wn4r, wk1r, wk1i, x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i, y0r, y0i, y1r, y1i, y2r, y2i, y3r, y3i, y4r, y4i, y5r, y5i, y6r, y6i, y7r, y7i, y8r, y8i, y9r, y9i, y10r, y10i, y11r, y11i, y12r, y12i, y13r, y13i, y14r, y14i, y15r, y15i;

        wn4r = w[wP + 1];
        wk1i = wn4r * w[wP + 2];
        wk1r = wk1i + w[wP + 2];
        x0r = a[aP + 0] + a[aP + 16];
        x0i = a[aP + 1] + a[aP + 17];
        x1r = a[aP + 0] - a[aP + 16];
        x1i = a[aP + 1] - a[aP + 17];
        x2r = a[aP + 8] + a[aP + 24];
        x2i = a[aP + 9] + a[aP + 25];
        x3r = a[aP + 8] - a[aP + 24];
        x3i = a[aP + 9] - a[aP + 25];
        y0r = x0r + x2r;
        y0i = x0i + x2i;
        y4r = x0r - x2r;
        y4i = x0i - x2i;
        y8r = x1r - x3i;
        y8i = x1i + x3r;
        y12r = x1r + x3i;
        y12i = x1i - x3r;
        x0r = a[aP + 2] + a[aP + 18];
        x0i = a[aP + 3] + a[aP + 19];
        x1r = a[aP + 2] - a[aP + 18];
        x1i = a[aP + 3] - a[aP + 19];
        x2r = a[aP + 10] + a[aP + 26];
        x2i = a[aP + 11] + a[aP + 27];
        x3r = a[aP + 10] - a[aP + 26];
        x3i = a[aP + 11] - a[aP + 27];
        y1r = x0r + x2r;
        y1i = x0i + x2i;
        y5r = x0r - x2r;
        y5i = x0i - x2i;
        x0r = x1r - x3i;
        x0i = x1i + x3r;
        y9r = wk1r * x0r - wk1i * x0i;
        y9i = wk1r * x0i + wk1i * x0r;
        x0r = x1r + x3i;
        x0i = x1i - x3r;
        y13r = wk1i * x0r - wk1r * x0i;
        y13i = wk1i * x0i + wk1r * x0r;
        x0r = a[aP + 4] + a[aP + 20];
        x0i = a[aP + 5] + a[aP + 21];
        x1r = a[aP + 4] - a[aP + 20];
        x1i = a[aP + 5] - a[aP + 21];
        x2r = a[aP + 12] + a[aP + 28];
        x2i = a[aP + 13] + a[aP + 29];
        x3r = a[aP + 12] - a[aP + 28];
        x3i = a[aP + 13] - a[aP + 29];
        y2r = x0r + x2r;
        y2i = x0i + x2i;
        y6r = x0r - x2r;
        y6i = x0i - x2i;
        x0r = x1r - x3i;
        x0i = x1i + x3r;
        y10r = wn4r * (x0r - x0i);
        y10i = wn4r * (x0i + x0r);
        x0r = x1r + x3i;
        x0i = x1i - x3r;
        y14r = wn4r * (x0r + x0i);
        y14i = wn4r * (x0i - x0r);
        x0r = a[aP + 6] + a[aP + 22];
        x0i = a[aP + 7] + a[aP + 23];
        x1r = a[aP + 6] - a[aP + 22];
        x1i = a[aP + 7] - a[aP + 23];
        x2r = a[aP + 14] + a[aP + 30];
        x2i = a[aP + 15] + a[aP + 31];
        x3r = a[aP + 14] - a[aP + 30];
        x3i = a[aP + 15] - a[aP + 31];
        y3r = x0r + x2r;
        y3i = x0i + x2i;
        y7r = x0r - x2r;
        y7i = x0i - x2i;
        x0r = x1r - x3i;
        x0i = x1i + x3r;
        y11r = wk1i * x0r - wk1r * x0i;
        y11i = wk1i * x0i + wk1r * x0r;
        x0r = x1r + x3i;
        x0i = x1i - x3r;
        y15r = wk1r * x0r - wk1i * x0i;
        y15i = wk1r * x0i + wk1i * x0r;
        x0r = y12r - y14r;
        x0i = y12i - y14i;
        x1r = y12r + y14r;
        x1i = y12i + y14i;
        x2r = y13r - y15r;
        x2i = y13i - y15i;
        x3r = y13r + y15r;
        x3i = y13i + y15i;
        a[aP + 24] = x0r + x2r;
        a[aP + 25] = x0i + x2i;
        a[aP + 26] = x0r - x2r;
        a[aP + 27] = x0i - x2i;
        a[aP + 28] = x1r - x3i;
        a[aP + 29] = x1i + x3r;
        a[aP + 30] = x1r + x3i;
        a[aP + 31] = x1i - x3r;
        x0r = y8r + y10r;
        x0i = y8i + y10i;
        x1r = y8r - y10r;
        x1i = y8i - y10i;
        x2r = y9r + y11r;
        x2i = y9i + y11i;
        x3r = y9r - y11r;
        x3i = y9i - y11i;
        a[aP + 16] = x0r + x2r;
        a[aP + 17] = x0i + x2i;
        a[aP + 18] = x0r - x2r;
        a[aP + 19] = x0i - x2i;
        a[aP + 20] = x1r - x3i;
        a[aP + 21] = x1i + x3r;
        a[aP + 22] = x1r + x3i;
        a[aP + 23] = x1i - x3r;
        x0r = y5r - y7i;
        x0i = y5i + y7r;
        x2r = wn4r * (x0r - x0i);
        x2i = wn4r * (x0i + x0r);
        x0r = y5r + y7i;
        x0i = y5i - y7r;
        x3r = wn4r * (x0r - x0i);
        x3i = wn4r * (x0i + x0r);
        x0r = y4r - y6i;
        x0i = y4i + y6r;
        x1r = y4r + y6i;
        x1i = y4i - y6r;
        a[aP + 8] = x0r + x2r;
        a[aP + 9] = x0i + x2i;
        a[aP + 10] = x0r - x2r;
        a[aP + 11] = x0i - x2i;
        a[aP + 12] = x1r - x3i;
        a[aP + 13] = x1i + x3r;
        a[aP + 14] = x1r + x3i;
        a[aP + 15] = x1i - x3r;
        x0r = y0r + y2r;
        x0i = y0i + y2i;
        x1r = y0r - y2r;
        x1i = y0i - y2i;
        x2r = y1r + y3r;
        x2i = y1i + y3i;
        x3r = y1r - y3r;
        x3i = y1i - y3i;
        a[aP + 0] = x0r + x2r;
        a[aP + 1] = x0i + x2i;
        a[aP + 2] = x0r - x2r;
        a[aP + 3] = x0i - x2i;
        a[aP + 4] = x1r - x3i;
        a[aP + 5] = x1i + x3r;
        a[aP + 6] = x1r + x3i;
        a[aP + 7] = x1i - x3r;
    }

    /** */
    private static void cftf162(float[] a, int aP, float[] w, int wP)
    {
        float wn4r, wk1r, wk1i, wk2r, wk2i, wk3r, wk3i, x0r, x0i, x1r, x1i, x2r, x2i, y0r, y0i, y1r, y1i, y2r, y2i, y3r, y3i, y4r, y4i, y5r, y5i, y6r, y6i, y7r, y7i, y8r, y8i, y9r, y9i, y10r, y10i, y11r, y11i, y12r, y12i, y13r, y13i, y14r, y14i, y15r, y15i;

        wn4r = w[wP + 1];
        wk1r = w[wP + 4];
        wk1i = w[wP + 5];
        wk3r = w[wP + 6];
        wk3i = w[wP + 7];
        wk2r = w[wP + 8];
        wk2i = w[wP + 9];
        x1r = a[aP + 0] - a[aP + 17];
        x1i = a[aP + 1] + a[aP + 16];
        x0r = a[aP + 8] - a[aP + 25];
        x0i = a[aP + 9] + a[aP + 24];
        x2r = wn4r * (x0r - x0i);
        x2i = wn4r * (x0i + x0r);
        y0r = x1r + x2r;
        y0i = x1i + x2i;
        y4r = x1r - x2r;
        y4i = x1i - x2i;
        x1r = a[aP + 0] + a[aP + 17];
        x1i = a[aP + 1] - a[aP + 16];
        x0r = a[aP + 8] + a[aP + 25];
        x0i = a[aP + 9] - a[aP + 24];
        x2r = wn4r * (x0r - x0i);
        x2i = wn4r * (x0i + x0r);
        y8r = x1r - x2i;
        y8i = x1i + x2r;
        y12r = x1r + x2i;
        y12i = x1i - x2r;
        x0r = a[aP + 2] - a[aP + 19];
        x0i = a[aP + 3] + a[aP + 18];
        x1r = wk1r * x0r - wk1i * x0i;
        x1i = wk1r * x0i + wk1i * x0r;
        x0r = a[aP + 10] - a[aP + 27];
        x0i = a[aP + 11] + a[aP + 26];
        x2r = wk3i * x0r - wk3r * x0i;
        x2i = wk3i * x0i + wk3r * x0r;
        y1r = x1r + x2r;
        y1i = x1i + x2i;
        y5r = x1r - x2r;
        y5i = x1i - x2i;
        x0r = a[aP + 2] + a[aP + 19];
        x0i = a[aP + 3] - a[aP + 18];
        x1r = wk3r * x0r - wk3i * x0i;
        x1i = wk3r * x0i + wk3i * x0r;
        x0r = a[aP + 10] + a[aP + 27];
        x0i = a[aP + 11] - a[aP + 26];
        x2r = wk1r * x0r + wk1i * x0i;
        x2i = wk1r * x0i - wk1i * x0r;
        y9r = x1r - x2r;
        y9i = x1i - x2i;
        y13r = x1r + x2r;
        y13i = x1i + x2i;
        x0r = a[aP + 4] - a[aP + 21];
        x0i = a[aP + 5] + a[aP + 20];
        x1r = wk2r * x0r - wk2i * x0i;
        x1i = wk2r * x0i + wk2i * x0r;
        x0r = a[aP + 12] - a[aP + 29];
        x0i = a[aP + 13] + a[aP + 28];
        x2r = wk2i * x0r - wk2r * x0i;
        x2i = wk2i * x0i + wk2r * x0r;
        y2r = x1r + x2r;
        y2i = x1i + x2i;
        y6r = x1r - x2r;
        y6i = x1i - x2i;
        x0r = a[aP + 4] + a[aP + 21];
        x0i = a[aP + 5] - a[aP + 20];
        x1r = wk2i * x0r - wk2r * x0i;
        x1i = wk2i * x0i + wk2r * x0r;
        x0r = a[aP + 12] + a[aP + 29];
        x0i = a[aP + 13] - a[aP + 28];
        x2r = wk2r * x0r - wk2i * x0i;
        x2i = wk2r * x0i + wk2i * x0r;
        y10r = x1r - x2r;
        y10i = x1i - x2i;
        y14r = x1r + x2r;
        y14i = x1i + x2i;
        x0r = a[aP + 6] - a[aP + 23];
        x0i = a[aP + 7] + a[aP + 22];
        x1r = wk3r * x0r - wk3i * x0i;
        x1i = wk3r * x0i + wk3i * x0r;
        x0r = a[aP + 14] - a[aP + 31];
        x0i = a[aP + 15] + a[aP + 30];
        x2r = wk1i * x0r - wk1r * x0i;
        x2i = wk1i * x0i + wk1r * x0r;
        y3r = x1r + x2r;
        y3i = x1i + x2i;
        y7r = x1r - x2r;
        y7i = x1i - x2i;
        x0r = a[aP + 6] + a[aP + 23];
        x0i = a[aP + 7] - a[aP + 22];
        x1r = wk1i * x0r + wk1r * x0i;
        x1i = wk1i * x0i - wk1r * x0r;
        x0r = a[aP + 14] + a[aP + 31];
        x0i = a[aP + 15] - a[aP + 30];
        x2r = wk3i * x0r - wk3r * x0i;
        x2i = wk3i * x0i + wk3r * x0r;
        y11r = x1r + x2r;
        y11i = x1i + x2i;
        y15r = x1r - x2r;
        y15i = x1i - x2i;
        x1r = y0r + y2r;
        x1i = y0i + y2i;
        x2r = y1r + y3r;
        x2i = y1i + y3i;
        a[aP + 0] = x1r + x2r;
        a[aP + 1] = x1i + x2i;
        a[aP + 2] = x1r - x2r;
        a[aP + 3] = x1i - x2i;
        x1r = y0r - y2r;
        x1i = y0i - y2i;
        x2r = y1r - y3r;
        x2i = y1i - y3i;
        a[aP + 4] = x1r - x2i;
        a[aP + 5] = x1i + x2r;
        a[aP + 6] = x1r + x2i;
        a[aP + 7] = x1i - x2r;
        x1r = y4r - y6i;
        x1i = y4i + y6r;
        x0r = y5r - y7i;
        x0i = y5i + y7r;
        x2r = wn4r * (x0r - x0i);
        x2i = wn4r * (x0i + x0r);
        a[aP + 8] = x1r + x2r;
        a[aP + 9] = x1i + x2i;
        a[aP + 10] = x1r - x2r;
        a[aP + 11] = x1i - x2i;
        x1r = y4r + y6i;
        x1i = y4i - y6r;
        x0r = y5r + y7i;
        x0i = y5i - y7r;
        x2r = wn4r * (x0r - x0i);
        x2i = wn4r * (x0i + x0r);
        a[aP + 12] = x1r - x2i;
        a[aP + 13] = x1i + x2r;
        a[aP + 14] = x1r + x2i;
        a[aP + 15] = x1i - x2r;
        x1r = y8r + y10r;
        x1i = y8i + y10i;
        x2r = y9r - y11r;
        x2i = y9i - y11i;
        a[aP + 16] = x1r + x2r;
        a[aP + 17] = x1i + x2i;
        a[aP + 18] = x1r - x2r;
        a[aP + 19] = x1i - x2i;
        x1r = y8r - y10r;
        x1i = y8i - y10i;
        x2r = y9r + y11r;
        x2i = y9i + y11i;
        a[aP + 20] = x1r - x2i;
        a[aP + 21] = x1i + x2r;
        a[aP + 22] = x1r + x2i;
        a[aP + 23] = x1i - x2r;
        x1r = y12r - y14i;
        x1i = y12i + y14r;
        x0r = y13r + y15i;
        x0i = y13i - y15r;
        x2r = wn4r * (x0r - x0i);
        x2i = wn4r * (x0i + x0r);
        a[aP + 24] = x1r + x2r;
        a[aP + 25] = x1i + x2i;
        a[aP + 26] = x1r - x2r;
        a[aP + 27] = x1i - x2i;
        x1r = y12r + y14i;
        x1i = y12i - y14r;
        x0r = y13r - y15i;
        x0i = y13i + y15r;
        x2r = wn4r * (x0r - x0i);
        x2i = wn4r * (x0i + x0r);
        a[aP + 28] = x1r - x2i;
        a[aP + 29] = x1i + x2r;
        a[aP + 30] = x1r + x2i;
        a[aP + 31] = x1i - x2r;
    }

    /** */
    private static void cftf081(float[] a, int aP, float[] w, int wP)
    {
        float wn4r, x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i, y0r, y0i, y1r, y1i, y2r, y2i, y3r, y3i, y4r, y4i, y5r, y5i, y6r, y6i, y7r, y7i;

        wn4r = w[wP + 1];
        x0r = a[aP + 0] + a[aP + 8];
        x0i = a[aP + 1] + a[aP + 9];
        x1r = a[aP + 0] - a[aP + 8];
        x1i = a[aP + 1] - a[aP + 9];
        x2r = a[aP + 4] + a[aP + 12];
        x2i = a[aP + 5] + a[aP + 13];
        x3r = a[aP + 4] - a[aP + 12];
        x3i = a[aP + 5] - a[aP + 13];
        y0r = x0r + x2r;
        y0i = x0i + x2i;
        y2r = x0r - x2r;
        y2i = x0i - x2i;
        y1r = x1r - x3i;
        y1i = x1i + x3r;
        y3r = x1r + x3i;
        y3i = x1i - x3r;
        x0r = a[aP + 2] + a[aP + 10];
        x0i = a[aP + 3] + a[aP + 11];
        x1r = a[aP + 2] - a[aP + 10];
        x1i = a[aP + 3] - a[aP + 11];
        x2r = a[aP + 6] + a[aP + 14];
        x2i = a[aP + 7] + a[aP + 15];
        x3r = a[aP + 6] - a[aP + 14];
        x3i = a[aP + 7] - a[aP + 15];
        y4r = x0r + x2r;
        y4i = x0i + x2i;
        y6r = x0r - x2r;
        y6i = x0i - x2i;
        x0r = x1r - x3i;
        x0i = x1i + x3r;
        x2r = x1r + x3i;
        x2i = x1i - x3r;
        y5r = wn4r * (x0r - x0i);
        y5i = wn4r * (x0r + x0i);
        y7r = wn4r * (x2r - x2i);
        y7i = wn4r * (x2r + x2i);
        a[aP + 8] = y1r + y5r;
        a[aP + 9] = y1i + y5i;
        a[aP + 10] = y1r - y5r;
        a[aP + 11] = y1i - y5i;
        a[aP + 12] = y3r - y7i;
        a[aP + 13] = y3i + y7r;
        a[aP + 14] = y3r + y7i;
        a[aP + 15] = y3i - y7r;
        a[aP + 0] = y0r + y4r;
        a[aP + 1] = y0i + y4i;
        a[aP + 2] = y0r - y4r;
        a[aP + 3] = y0i - y4i;
        a[aP + 4] = y2r - y6i;
        a[aP + 5] = y2i + y6r;
        a[aP + 6] = y2r + y6i;
        a[aP + 7] = y2i - y6r;
    }

    /** */
    private static void cftf082(float[] a, int aP, float[] w, int wP)
    {
        float wn4r, wk1r, wk1i, x0r, x0i, x1r, x1i, y0r, y0i, y1r, y1i, y2r, y2i, y3r, y3i, y4r, y4i, y5r, y5i, y6r, y6i, y7r, y7i;

        wn4r = w[wP + 1];
        wk1r = w[wP + 4];
        wk1i = w[wP + 5];
        y0r = a[aP + 0] - a[aP + 9];
        y0i = a[aP + 1] + a[aP + 8];
        y1r = a[aP + 0] + a[aP + 9];
        y1i = a[aP + 1] - a[aP + 8];
        x0r = a[aP + 4] - a[aP + 13];
        x0i = a[aP + 5] + a[aP + 12];
        y2r = wn4r * (x0r - x0i);
        y2i = wn4r * (x0i + x0r);
        x0r = a[aP + 4] + a[aP + 13];
        x0i = a[aP + 5] - a[aP + 12];
        y3r = wn4r * (x0r - x0i);
        y3i = wn4r * (x0i + x0r);
        x0r = a[aP + 2] - a[aP + 11];
        x0i = a[aP + 3] + a[aP + 10];
        y4r = wk1r * x0r - wk1i * x0i;
        y4i = wk1r * x0i + wk1i * x0r;
        x0r = a[aP + 2] + a[aP + 11];
        x0i = a[aP + 3] - a[aP + 10];
        y5r = wk1i * x0r - wk1r * x0i;
        y5i = wk1i * x0i + wk1r * x0r;
        x0r = a[aP + 6] - a[aP + 15];
        x0i = a[aP + 7] + a[aP + 14];
        y6r = wk1i * x0r - wk1r * x0i;
        y6i = wk1i * x0i + wk1r * x0r;
        x0r = a[aP + 6] + a[aP + 15];
        x0i = a[aP + 7] - a[aP + 14];
        y7r = wk1r * x0r - wk1i * x0i;
        y7i = wk1r * x0i + wk1i * x0r;
        x0r = y0r + y2r;
        x0i = y0i + y2i;
        x1r = y4r + y6r;
        x1i = y4i + y6i;
        a[aP + 0] = x0r + x1r;
        a[aP + 1] = x0i + x1i;
        a[aP + 2] = x0r - x1r;
        a[aP + 3] = x0i - x1i;
        x0r = y0r - y2r;
        x0i = y0i - y2i;
        x1r = y4r - y6r;
        x1i = y4i - y6i;
        a[aP + 4] = x0r - x1i;
        a[aP + 5] = x0i + x1r;
        a[aP + 6] = x0r + x1i;
        a[aP + 7] = x0i - x1r;
        x0r = y1r - y3i;
        x0i = y1i + y3r;
        x1r = y5r - y7r;
        x1i = y5i - y7i;
        a[aP + 8] = x0r + x1r;
        a[aP + 9] = x0i + x1i;
        a[aP + 10] = x0r - x1r;
        a[aP + 11] = x0i - x1i;
        x0r = y1r + y3i;
        x0i = y1i - y3r;
        x1r = y5r + y7r;
        x1i = y5i + y7i;
        a[aP + 12] = x0r - x1i;
        a[aP + 13] = x0i + x1r;
        a[aP + 14] = x0r + x1i;
        a[aP + 15] = x0i - x1r;
    }

    /**
     * 3rd
     * when n = 8.
     * @see #cftfsub(int, float[], int[], int, int, float[])
     */
    private static void cftf040(float[] a)
    {
        float x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i;

        x0r = a[0] + a[4];
        x0i = a[1] + a[5];
        x1r = a[0] - a[4];
        x1i = a[1] - a[5];
        x2r = a[2] + a[6];
        x2i = a[3] + a[7];
        x3r = a[2] - a[6];
        x3i = a[3] - a[7];
        a[0] = x0r + x2r;
        a[1] = x0i + x2i;
        a[4] = x0r - x2r;
        a[5] = x0i - x2i;
        a[2] = x1r - x3i;
        a[3] = x1i + x3r;
        a[6] = x1r + x3i;
        a[7] = x1i - x3r;
    }

    /**
     * 3rd
     * when n = 8.
     * @see #cftbsub(int, float[], int[], int, int, float[])
     */
    private static void cftb040(float[] a)
    {
        float x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i;

        x0r = a[0] + a[4];
        x0i = a[1] + a[5];
        x1r = a[0] - a[4];
        x1i = a[1] - a[5];
        x2r = a[2] + a[6];
        x2i = a[3] + a[7];
        x3r = a[2] - a[6];
        x3i = a[3] - a[7];
        a[0] = x0r + x2r;
        a[1] = x0i + x2i;
        a[4] = x0r - x2r;
        a[5] = x0i - x2i;
        a[2] = x1r + x3i;
        a[3] = x1i - x3r;
        a[6] = x1r - x3i;
        a[7] = x1i + x3r;
    }

    /**
     * 3rd
     * when n = 4.
     * @see #cftbsub(int, float[], int[], int, int, float[])
     * @see #cftfsub(int, float[], int[], int, int, float[])
     */
    private static void cftx020(float[] a)
    {
        float x0r, x0i;

        x0r = a[0] - a[2];
        x0i = a[1] - a[3];
        a[0] += a[2];
        a[1] += a[3];
        a[2] = x0r;
        a[3] = x0i;
    }

    /**
     * 2nd
     * @see #rdft(int, int, float[], int[], float[])
     * @see #ddct(int, int, float[], int[], float[])
     * @see #ddst(int, int, float[], int[], float[])
     * @see #dfst(int, float[], float[], int[], float[])
     * @see #dfct(int, float[], float[], int[], float[])
     */
    private static void rftfsub(int n, float[] a, int nc, float[] c, int cP)
    {
        int j, k, kk, ks, m;
        float wkr, wki, xr, xi, yr, yi;

        m = n >> 1;
        ks = 2 * nc / m;
        kk = 0;
        for (j = 2; j < m; j += 2)
        {
            k = n - j;
            kk += ks;
            wkr = 0.5f - c[cP + nc - kk];
            wki = c[cP + kk];
            xr = a[j] - a[k];
            xi = a[j + 1] + a[k + 1];
            yr = wkr * xr - wki * xi;
            yi = wkr * xi + wki * xr;
            a[j] -= yr;
            a[j + 1] -= yi;
            a[k] += yr;
            a[k + 1] -= yi;
        }
    }

    /**
     * 2nd
     * @see #rdft(int, int, float[], int[], float[])
     * @see #ddct(int, int, float[], int[], float[])
     * @see #ddst(int, int, float[], int[], float[])
     */
    private static void rftbsub(int n, float[] a, int nc, float[] c, int cP)
    {
        int j, k, kk, ks, m;
        float wkr, wki, xr, xi, yr, yi;

        m = n >> 1;
        ks = 2 * nc / m;
        kk = 0;
        for (j = 2; j < m; j += 2)
        {
            k = n - j;
            kk += ks;
            wkr = 0.5f - c[cP + nc - kk];
            wki = c[cP + kk];
            xr = a[j] - a[k];
            xi = a[j + 1] + a[k + 1];
            yr = wkr * xr + wki * xi;
            yi = wkr * xi - wki * xr;
            a[j] -= yr;
            a[j + 1] -= yi;
            a[k] += yr;
            a[k + 1] -= yi;
        }
    }
}

/* */