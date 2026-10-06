// Fast-forward (SPEC 4): jump a long time in one call. Stepping costs the same per year however quiet the system
// is; life needs millions of years. In a jump every object rides the orbit it is on right now ("rails"), by the
// closed two-body formula, so the cost does not depend on the length of the jump.
//   primary  = the lightest pulling object whose zone (Hill radius) holds the object AND that it is bound to,
//              else the heaviest object.
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
    double[]? _mean, _hill, _sm, _bx, _by, _bvx, _bvy, _cx, _cy, _cvx, _cvy;
    Comparer<int>? _heavyFirst;
    bool _deferRocks;

    // A long jump is cut into chunks and the rule table runs after each, so water, life and civilisation see the
    // years go by instead of one leap. Chunks are never shorter than the fastest rule needs, never more than
    // JumpSamples (the cost of a jump is chunks * objects).
    void Jump(double t)
    {
        double fastest = double.MaxValue;
        foreach (Rule r in Rules) if (r.Enabled) fastest = Math.Min(fastest, r.RhythmYears);
        double years = t / C.YearTime;
        int chunks = (int)Math.Clamp(Math.Ceiling(years / fastest), 1, Math.Max(1, C.JumpSamples));
        bool defer = chunks > 1 && Rules.Count == _builtinRules.Length;
        for (int i = 0; defer && i < Rules.Count; i++) defer &= ReferenceEquals(Rules[i], _builtinRules[i]);
        bool hasRocks = false;
        for (int i = 0; defer && i < N; i++) if (Alive[i] && !Attracts(i)) { hasRocks = true; break; }
        defer &= hasRocks;
        if (defer)
        {
            Ride(0); // choose the same initial primary hierarchy and retain each rock's relative orbit
            for (int i = 0; i < N; i++) if (Alive[i] && !Attracts(i))
            {
                // Rock scratch cells are unused by body-only Ride; keep inertial starts for escape trajectories.
                _cx![i] = X[i]; _cy![i] = Y[i]; _cvx![i] = Vx[i]; _cvy![i] = Vy[i];
            }
        }
        _riding = true;
        _deferRocks = defer;
        try
        {
            for (int k = 0; k < chunks; k++)
            {
                Ride(t / chunks);
                if (defer && k == chunks - 1) { FinishRocks(t); _deferRocks = false; }
                Year += years / chunks; RunRules();
            }
        }
        finally { _riding = _deferRocks = false; }
    }

    // ponytail: rocks exert no pull, so omit their lump recoil during body chunks. Their initial primary
    // is held for this jump; future rules that need rock positions use the untrimmed path above.
    // No-rock worlds and single-chunk jumps retain the original operation order and exact hashes.
    void FinishRocks(double t)
    {
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || Attracts(i)) continue;
            int p = _prim![i];
            if (p < 0 || !Kepler(ref _bx![i], ref _by![i], ref _bvx![i], ref _bvy![i], C.G * (M[p] + M[i]), t, out _mean![i]))
            {
                X[i] = _cx![i] + _cvx![i] * t; Y[i] = _cy![i] + _cvy![i] * t;
                Vx[i] = _cvx[i]; Vy[i] = _cvy[i];
                if (p >= 0) OffRails++;
                _mean![i] = double.NaN;
            }
            else
            {
                X[i] = X[p] + _bx[i]; Y[i] = Y[p] + _by[i];
                Vx[i] = Vx[p] + _bvx[i]; Vy[i] = Vy[p] + _bvy[i];
            }
        }
    }

    // During a jump a planet is looked at once per chunk, at whatever point of its orbit it happens to be. On a
    // stretched orbit that would read hot, cold, hot, cold by chance. So in a jump the light of the star an object
    // (or the body it rides around) orbits is taken as the average over one whole turn: 1 / (a * a * sqrt(1 - e * e)).
    // Returns that star and the averaged 1/distance^2, or -1 when the object is not on a closed orbit around a star.
    bool _riding;
    int RailStar(int i, out double meanInvD2)
    {
        meanInvD2 = 0;
        if (!_riding) return -1;
        for (int k = i, p; (p = _prim![k]) >= 0; k = p)
            if (M[p] >= C.StarMass) { meanInvD2 = _mean![k]; return double.IsNaN(meanInvD2) ? -1 : p; }
        return -1;
    }

    void Ride(double t)
    {
        int cap = X.Length;
        if (_prim == null)
        {
            _prim = new int[cap]; _ord = new int[cap]; _mean = new double[cap]; _hill = new double[cap]; _sm = new double[cap];
            _bx = new double[cap]; _by = new double[cap]; _bvx = new double[cap]; _bvy = new double[cap];
            _cx = new double[cap]; _cy = new double[cap]; _cvx = new double[cap]; _cvy = new double[cap];
            _heavyFirst = Comparer<int>.Create((a, b) => M[a] != M[b] ? M[b].CompareTo(M[a]) : a.CompareTo(b));
        }
        int[] prim = _prim, ord = _ord!;
        double[] mean = _mean!, hill = _hill!, sm = _sm!, bx = _bx!, by = _by!, bvx = _bvx!, bvy = _bvy!, cx = _cx!, cy = _cy!, cvx = _cvx!, cvy = _cvy!;
        OffRails = 0;

        int na = 0;
        for (int i = 0; i < N; i++) if (Alive[i] && Attracts(i)) ord[na++] = i;
        if (na == 0) { for (int i = 0; i < N; i++) if (Alive[i] && !_deferRocks) { X[i] += Vx[i] * t; Y[i] += Vy[i] * t; prim[i] = -1; } return; }
        Array.Sort(ord, 0, na, _heavyFirst); // a primary is always heavier than what it holds: it comes first

        // who rides around whom. `upTo` = how many of the pulling objects (heaviest first) may be the primary.
        int primaryOf(int i, int upTo)
        {
            for (int k = upTo - 1; k > 0; k--)
            {
                int j = ord[k];
                double dx = X[i] - X[j], dy = Y[i] - Y[j], d2 = dx * dx + dy * dy;
                if (d2 >= hill[j] * hill[j]) continue;
                // only passing through the zone (too fast to be held) = still rides around the one further up;
                // otherwise a fly-by caught at this instant would be carried off on a straight line for the whole jump
                double ux = Vx[i] - Vx[j], uy = Vy[i] - Vy[j];
                if (ux * ux + uy * uy < 2 * C.G * (M[i] + M[j]) / Math.Sqrt(d2)) return j;
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
            if (!Alive[i] || _deferRocks && !Attracts(i)) continue;
            sm[i] = M[i]; bx[i] = M[i] * X[i]; by[i] = M[i] * Y[i]; bvx[i] = M[i] * Vx[i]; bvy[i] = M[i] * Vy[i];
            cx[i] = cy[i] = cvx[i] = cvy[i] = 0;
        }
        void into(int i, int p) { sm[p] += sm[i]; bx[p] += bx[i]; by[p] += by[i]; bvx[p] += bvx[i]; bvy[p] += bvy[i]; }
        for (int i = 0; !_deferRocks && i < N; i++) if (Alive[i] && !Attracts(i)) { prim[i] = primaryOf(i, na); into(i, prim[i]); }
        for (int k = na - 1; k > 0; k--) into(ord[k], prim[ord[k]]); // lightest first: a lump is whole before it is added on
        for (int i = 0; i < N; i++) if (Alive[i] && (!_deferRocks || Attracts(i))) { bx[i] /= sm[i]; by[i] /= sm[i]; bvx[i] /= sm[i]; bvy[i] /= sm[i]; }

        // every lump: its place seen from its primary, moved along the orbit. Nothing in X.. changes yet.
        for (int i = 0; i < N; i++)
        {
            if (!Alive[i] || _deferRocks && !Attracts(i)) continue;
            int p = prim[i];
            if (p < 0) { bx[i] += bvx[i] * t; by[i] += bvy[i] * t; mean[i] = double.NaN; continue; } // the whole system drifts
            bx[i] -= X[p]; by[i] -= Y[p]; bvx[i] -= Vx[p]; bvy[i] -= Vy[p];
            if (!Kepler(ref bx[i], ref by[i], ref bvx[i], ref bvy[i], C.G * (M[p] + sm[i]), t, out mean[i])) OffRails++;
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
        for (int i = 0; !_deferRocks && i < N; i++) if (Alive[i] && !Attracts(i)) place(i);
    }

    // Two-body motion of (x, y, vx, vy) around a centre of strength mu = G * mass, over time t. Closed orbits only:
    // returns false and moves on a straight line when the object is not bound.
    // Works on the change of the eccentric angle, so a perfect circle needs no special case; whole turns are
    // dropped first, so a million years costs the same as a day.
    // meanInvD2 = 1/distance^2 averaged over one turn (NaN when not bound).
    static bool Kepler(ref double x, ref double y, ref double vx, ref double vy, double mu, double t, out double meanInvD2)
    {
        meanInvD2 = double.NaN;
        double r0 = Math.Sqrt(x * x + y * y), inva = 2 / r0 - (vx * vx + vy * vy) / mu;
        if (!(inva > 0) || !(mu > 0)) { x += vx * t; y += vy * t; return false; }
        double a = 1 / inva, n = Math.Sqrt(mu * inva * inva * inva);
        double dm = Math.IEEERemainder(n * t, Math.Tau);
        double ec = 1 - r0 * inva, es = (x * vx + y * vy) / (n * a * a); // e*cos(E0), e*sin(E0)
        meanInvD2 = inva * inva / Math.Sqrt(Math.Max(1e-12, 1 - ec * ec - es * es));
        // Newton, kept inside a bracket: the left side only ever rises with de and differs from de by at most 2,
        // so the answer lies in dm-2 .. dm+2. On a very stretched orbit plain Newton can jump out and never return.
        double de = dm, lo = dm - 2, hi = dm + 2, s, c;
        for (int it = 0; it < 80; it++)
        {
            s = Math.Sin(de); c = Math.Cos(de);
            double f0 = de - ec * s + es * (1 - c) - dm;
            if (f0 > 0) hi = de; else lo = de;
            double next = de - f0 / (1 - ec * c + es * s);
            if (!(next > lo && next < hi)) next = 0.5 * (lo + hi);
            if (Math.Abs(next - de) < 1e-15) { de = next; break; }
            de = next;
        }
        s = Math.Sin(de); c = Math.Cos(de);
        double r = a * (1 - ec * c + es * s);
        double f = 1 - a / r0 * (1 - c), g = (dm + s - de) / n;
        double fd = -a * a * n * s / (r * r0), gd = 1 - a / r * (1 - c);
        double nx = f * x + g * vx, ny = f * y + g * vy, nvx = fd * x + gd * vx, nvy = fd * y + gd * vy;
        x = nx; y = ny; vx = nvx; vy = nvy;
        return true;
    }
}
