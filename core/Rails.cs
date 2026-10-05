// Fast-forward (SPEC 4): jump a long time in one call. Stepping costs the same per year however quiet the system
// is; life needs millions of years. In a jump every object rides the orbit it is on right now ("rails"), by the
// closed two-body formula, so the cost does not depend on the length of the jump.
//   primary  = the lightest pulling object whose zone (Hill radius) holds the object, else the heaviest object.
//   a body with satellites rides as one lump: the lump's centre of mass orbits the primary, the satellites orbit
//   the body; the body's own place is worked back from the two (so Earth does not carry the Moon's wobble along).
// Given up during a jump, on purpose: pull between siblings (Jupiter on Mars), collisions, anything that crosses.
// There is no rails "mode": after the jump the state is ordinary, the next Advance pulls for real again.
using System;
using System.Collections.Generic;

namespace Cosmos.Core;

public sealed partial class World
{
    /// Objects that were not on a closed orbit in the last jump; they moved on a straight line instead.
    public int OffRails;

    int[]? _prim, _ord;
    double[]? _hill, _sm, _bx, _by, _bvx, _bvy, _cx, _cy, _cvx, _cvy;
    Comparer<int>? _heavyFirst;

    void Jump(double t)
    {
        int cap = X.Length;
        if (_prim == null)
        {
            _prim = new int[cap]; _ord = new int[cap]; _hill = new double[cap]; _sm = new double[cap];
            _bx = new double[cap]; _by = new double[cap]; _bvx = new double[cap]; _bvy = new double[cap];
            _cx = new double[cap]; _cy = new double[cap]; _cvx = new double[cap]; _cvy = new double[cap];
            _heavyFirst = Comparer<int>.Create((a, b) => M[a] != M[b] ? M[b].CompareTo(M[a]) : a.CompareTo(b));
        }
        int[] prim = _prim, ord = _ord!;
        double[] hill = _hill!, sm = _sm!, bx = _bx!, by = _by!, bvx = _bvx!, bvy = _bvy!, cx = _cx!, cy = _cy!, cvx = _cvx!, cvy = _cvy!;
        OffRails = 0;
        Year += t / C.YearTime;

        int na = 0;
        for (int i = 0; i < N; i++) if (Alive[i] && Attracts(i)) ord[na++] = i;
        if (na == 0) { for (int i = 0; i < N; i++) if (Alive[i]) { X[i] += Vx[i] * t; Y[i] += Vy[i] * t; } RunRules(); return; }
        Array.Sort(ord, 0, na, _heavyFirst); // a primary is always heavier than what it holds: it comes first

        // who rides around whom. `upTo` = how many of the pulling objects (heaviest first) may be the primary.
        int primaryOf(int i, int upTo)
        {
            for (int k = upTo - 1; k > 0; k--)
            {
                int j = ord[k];
                double dx = X[i] - X[j], dy = Y[i] - Y[j];
                if (dx * dx + dy * dy < hill[j] * hill[j]) return j;
            }
            return ord[0];
        }
        int root = ord[0];
        prim[root] = -1; hill[root] = double.MaxValue;
        for (int k = 1; k < na; k++)
        {
            int i = ord[k], p = primaryOf(i, k);
            double dx = X[i] - X[p], dy = Y[i] - Y[p];
            prim[i] = p; hill[i] = Math.Sqrt(dx * dx + dy * dy) * Math.Cbrt(M[i] / (3 * M[p]));
        }

        // lump = an object plus everything that rides around it: mass, centre of mass, its velocity
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            sm[i] = M[i]; bx[i] = M[i] * X[i]; by[i] = M[i] * Y[i]; bvx[i] = M[i] * Vx[i]; bvy[i] = M[i] * Vy[i];
            cx[i] = cy[i] = cvx[i] = cvy[i] = 0;
        }
        void into(int i, int p) { sm[p] += sm[i]; bx[p] += bx[i]; by[p] += by[i]; bvx[p] += bvx[i]; bvy[p] += bvy[i]; }
        for (int i = 0; i < N; i++) if (Alive[i] && !Attracts(i)) { prim[i] = primaryOf(i, na); into(i, prim[i]); }
        for (int k = na - 1; k > 0; k--) into(ord[k], prim[ord[k]]); // lightest first: a lump is whole before it is added on
        for (int i = 0; i < N; i++) if (Alive[i]) { bx[i] /= sm[i]; by[i] /= sm[i]; bvx[i] /= sm[i]; bvy[i] /= sm[i]; }

        // every lump: its place seen from its primary, moved along the orbit. Nothing in X.. changes yet.
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i]) continue;
            int p = prim[i];
            if (p < 0) { bx[i] += bvx[i] * t; by[i] += bvy[i] * t; continue; } // the whole system drifts
            bx[i] -= X[p]; by[i] -= Y[p]; bvx[i] -= Vx[p]; bvy[i] -= Vy[p];
            if (!Kepler(ref bx[i], ref by[i], ref bvx[i], ref bvy[i], C.G * (M[p] + sm[i]), t)) OffRails++;
            cx[p] += sm[i] * bx[i]; cy[p] += sm[i] * by[i]; cvx[p] += sm[i] * bvx[i]; cvy[p] += sm[i] * bvy[i];
        }

        // back to places, heaviest first: body = centre of its lump minus what its satellites weigh in
        void place(int i)
        {
            int p = prim[i];
            double ox = p < 0 ? 0 : X[p], oy = p < 0 ? 0 : Y[p], ovx = p < 0 ? 0 : Vx[p], ovy = p < 0 ? 0 : Vy[p];
            X[i] = ox + bx[i] - cx[i] / sm[i]; Y[i] = oy + by[i] - cy[i] / sm[i];
            Vx[i] = ovx + bvx[i] - cvx[i] / sm[i]; Vy[i] = ovy + bvy[i] - cvy[i] / sm[i];
        }
        for (int k = 0; k < na; k++) place(ord[k]);
        for (int i = 0; i < N; i++) if (Alive[i] && !Attracts(i)) place(i);
        RunRules(); // one run at the landing year; rules inside a jump = SPEC 4 OPEN
    }

    // Two-body motion of (x, y, vx, vy) around a centre of strength mu = G * mass, over time t. Closed orbits only:
    // returns false and moves on a straight line when the object is not bound.
    // Works on the change of the eccentric angle, so a perfect circle needs no special case; whole turns are
    // dropped first, so a million years costs the same as a day.
    static bool Kepler(ref double x, ref double y, ref double vx, ref double vy, double mu, double t)
    {
        double r0 = Math.Sqrt(x * x + y * y), inva = 2 / r0 - (vx * vx + vy * vy) / mu;
        if (!(inva > 0) || !(mu > 0)) { x += vx * t; y += vy * t; return false; }
        double a = 1 / inva, n = Math.Sqrt(mu * inva * inva * inva);
        double dm = Math.IEEERemainder(n * t, Math.Tau);
        double ec = 1 - r0 * inva, es = (x * vx + y * vy) / (n * a * a); // e*cos(E0), e*sin(E0)
        double de = dm, s = 0, c = 1;
        for (int it = 0; it < 40; it++)
        {
            s = Math.Sin(de); c = Math.Cos(de);
            double d = (de - ec * s + es * (1 - c) - dm) / (1 - ec * c + es * s);
            de -= d;
            if (Math.Abs(d) < 1e-15) break;
        }
        // ponytail: plain Newton; an orbit stretched past e ~ 0.97 can need a bracketed solver.
        s = Math.Sin(de); c = Math.Cos(de);
        double r = a * (1 - ec * c + es * s);
        double f = 1 - a / r0 * (1 - c), g = (dm + s - de) / n;
        double fd = -a * a * n * s / (r * r0), gd = 1 - a / r * (1 - c);
        double nx = f * x + g * vx, ny = f * y + g * vy, nvx = fd * x + gd * vx, nvy = fd * y + gd * vy;
        x = nx; y = ny; vx = nvx; vy = nvy;
        return true;
    }
}
