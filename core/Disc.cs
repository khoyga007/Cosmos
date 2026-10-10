// Heat Law, first slice (SPEC §15): matter in its second representation, and the first sink.
//
// A DISC is hot debris held by one host. It is not a list of fragments: it is mass per element group, spread over
// rings. Ring k holds the matter whose specific angular momentum about the host is DiscStep^(Base+k); its radius
// follows from the host, r = j^2 / (G*M_host). So mass, matter and angular momentum are exact by construction, and
// the radius moves by itself when the host grows or the god turns G.
//
// Law (Lynden-Bell & Pringle 1974, in discrete form). Friction inside the disc moves matter between neighbouring
// rings. For every amount that steps inward, an amount 1/DiscStep steps outward, so the pair carries no net angular
// momentum. The pair always ends lower in orbital energy; that difference is heat, and it is radiated. Nearly all
// the mass walks in and is swallowed at the inner edge (surface, or the last stable orbit of a black hole); a small
// tail walks out carrying the angular momentum. Ledgers below let a check close M, matter, L and E.
//
// One step is solved implicitly (tridiagonal), so a step of a second and a jump of a million years use the same
// code, stay positive and conserve exactly.
//
// Known limits of this slice (each is a debt, none is hidden):
//   - viscous time = DiscViscousOrbits orbits everywhere (alpha-disc figure for hot gas). Not yet a function of the
//     disc's own heat; the disc does not cool back into rocks. Only debris heated past DiscVaporEnergy enters.
//   - the hop rate is scaled so that spreading over one radius takes one viscous time; the O(1) factor against the
//     analytic solution is unverified.
//   - bodies feel the disc as extra mass at the host (far field). Wrong for a body flying through the disc.
//   - a host has no spin: accreted angular momentum goes to a ledger. Orbits inside a jump (Rails) read M only.
//   - outward tail past the Roche limit should clump back into moons. Not built: it stays disc.
//   - a host removed without an heir leaves its disc frozen in place.
using System;
using System.Collections.Generic;

namespace Cosmos.Core;

public sealed partial class Consts
{
    public double DiscOn = 1;               // 0 = hot debris becomes fragments as before this law
    public double DiscViscousOrbits = 100;  // [P] orbits for a ring to spread over its own radius; alpha 0.1, H/r 0.13
    // [P] heat per mass that turns debris to vapour. Silicate ~1e7 J/kg; one speed unit = 28.3 km/s (Earth's orbit).
    public double DiscVaporEnergy = 0.0125;
    public double DiscInnerOrbit = 3;       // [P] black hole: last stable orbit in Schwarzschild radii
}

public sealed class Disc
{
    public int Host { get; internal set; }   // -1 = host gone with no heir: frozen
    public int HostGen { get; internal set; }
    public int Sense { get; internal set; }  // +1 turns like the planets, -1 against
    public int Base { get; internal set; }   // ring 0 has specific angular momentum DiscStep^Base
    public int Cells { get; internal set; }
    public double Year { get; internal set; }  // evolved up to here
    public double Total { get; internal set; } // mass in the disc now
    public double Fed { get; internal set; }   // mass ever put in
    public bool Cold { get; internal set; }    // a belt: rock and ice, no gas friction, rings stay where they are
    public double Heat { get; internal set; }  // cold only: orbital energy its members had above circular orbits
    internal double[] Matter = Array.Empty<double>(); // Cells x element groups
}

public sealed partial class World
{
    public const double DiscStep = 1.189207115002721; // 2^(1/4): each ring is sqrt(2) wider than the one inside
    const int DiscHeadroom = 8, DiscMaxCells = 512;
    const double DiscMaxHop = 1e12; // a ring this fast is empty after one step; keeps the solve finite
    static readonly double DiscLnStep = Math.Log(DiscStep);

    readonly List<Disc> _discs = new();
    public IReadOnlyList<Disc> Discs => _discs;
    public double DiscRadiatedEnergy { get; private set; }  // heat made by friction; left the world as light
    public double DiscAccretedMass { get; private set; }
    public double DiscAccretedAngular { get; private set; } // went into host spin, which the core does not model
    public double DiscAccretedEnergy { get; private set; }  // orbital energy the swallowed matter still had
    public double DiscResidualAngular { get; private set; } // numerical: deposit that could not be placed exactly
    public double DiscResidualEnergy { get; private set; }  // numerical: one orbit split over two rings
    double[] _discIn = Array.Empty<double>(), _discDen = Array.Empty<double>(), _discCp = Array.Empty<double>(),
        _discX = Array.Empty<double>(), _discGain = Array.Empty<double>();

    static double DiscSpin(int index) => Math.Pow(DiscStep, index);

    public double DiscCellRadius(Disc d, int k)
    {
        double j = DiscSpin(d.Base + k), gm = d.Host >= 0 ? C.G * M[d.Host] : 0;
        return gm > 0 ? j * j / gm : 0;
    }

    public double DiscCellMatter(Disc d, int k, int element) => d.Matter[k * ElementCount + element];

    public double DiscCellMass(Disc d, int k)
    {
        double sum = 0;
        for (int e = 0; e < ElementCount; e++) sum += d.Matter[k * ElementCount + e];
        return sum;
    }

    /// Disc mass riding on this object. Gravity adds it to the object's pull.
    public double DiscMassOn(int host)
    {
        if (_discs.Count == 0) return 0;
        double sum = 0;
        foreach (var d in _discs) if (d.Host == host) sum += d.Total;
        return sum;
    }

    /// Angular momentum of the disc about its host, signed.
    public double DiscAngular(Disc d)
    {
        double sum = 0;
        for (int k = 0; k < d.Cells; k++) sum += DiscCellMass(d, k) * DiscSpin(d.Base + k);
        return d.Sense * sum;
    }

    /// Orbital energy (kinetic + potential) of the disc in the host's frame.
    public double DiscEnergy(Disc d)
    {
        double gm = d.Host >= 0 ? C.G * M[d.Host] : 0, sum = 0;
        if (!(gm > 0)) return 0;
        for (int k = 0; k < d.Cells; k++) { double j = DiscSpin(d.Base + k); sum -= gm * gm * DiscCellMass(d, k) / (2 * j * j); }
        return sum;
    }

    double DiscSink(int host) => R[host] * (StarPhaseOf(host) == StarPhase.BlackHole ? Math.Max(1, C.DiscInnerOrbit) : 1);

    void AccreteDisc(int host, double[] matter, double angular, double energy)
    {
        double mass = 0;
        for (int e = 0; e < ElementCount; e++) { Comp[host * ElementCount + e] += matter[e]; mass += matter[e]; }
        M[host] += mass; SetRadius(host);
        DiscAccretedMass += mass; DiscAccretedAngular += angular; DiscAccretedEnergy += energy;
    }

    // Make the rings [low, high] (absolute indices) exist. Never drops matter.
    void CoverDisc(Disc d, int low, int high)
    {
        int newBase = Math.Min(d.Base, low), top = Math.Max(d.Base + d.Cells - 1, high), count = top - newBase + 1;
        if (newBase == d.Base && count == d.Cells) return;
        if (count > DiscMaxCells) { if (d.Cells > 0) return; count = DiscMaxCells; newBase = top - count + 1; }
        var matter = new double[count * ElementCount];
        Array.Copy(d.Matter, 0, matter, (d.Base - newBase) * ElementCount, d.Cells * ElementCount);
        d.Matter = matter; d.Base = newBase; d.Cells = count;
    }

    // Hot bound debris joins the host's disc on the circular orbit that has its angular momentum `spin` (per mass,
    // signed). The caller has already taken the energy above that orbit out as heat.
    Disc? DepositDisc(int host, double[] matter, double spin, bool cold = false)
    {
        double mass = 0;
        for (int e = 0; e < ElementCount; e++) mass += matter[e];
        if (!(mass > 0)) return null;
        for (int n = _discs.Count - 1; n >= 0; n--) if (_discs[n].Host == host) EvolveDisc(_discs[n]);
        int sense = spin < 0 ? -1 : 1;
        double j = Math.Abs(spin), gm = C.G * M[host], sink = DiscSink(host);
        if (!(gm > 0) || !(j * j / gm > sink))
        {
            // its orbit lies inside the host: it falls straight in
            AccreteDisc(host, matter, sense * j * mass, gm > 0 && j > 0 ? -gm * gm * mass / (2 * j * j) : 0);
            return null;
        }
        Disc? disc = null;
        foreach (var d in _discs) if (d.Host == host && d.Sense == sense && d.Cold == cold) { disc = d; break; }
        int low = (int)Math.Floor(Math.Log(j) / DiscLnStep);
        if (disc == null)
        {
            int edge = (int)Math.Floor(Math.Log(Math.Sqrt(gm * sink)) / DiscLnStep);
            disc = new Disc { Host = host, HostGen = Gen[host], Sense = sense, Base = cold ? low : Math.Min(edge, low), Year = Year, Cold = cold };
            _discs.Add(disc);
            if (!cold) LogEvent(host, "disc", "disc.form", mass, j * j / gm);
        }
        CoverDisc(disc, low, low + 1 + (cold ? 0 : DiscHeadroom));
        int k = Math.Clamp(low - disc.Base, 0, disc.Cells - 2);
        double j0 = DiscSpin(disc.Base + k), j1 = DiscSpin(disc.Base + k + 1), share = Math.Clamp((j - j0) / (j1 - j0), 0, 1);
        for (int e = 0; e < ElementCount; e++)
        {
            disc.Matter[k * ElementCount + e] += matter[e] * (1 - share);
            disc.Matter[(k + 1) * ElementCount + e] += matter[e] * share;
        }
        disc.Total += mass; disc.Fed += mass;
        // Two rings carry the mass and the angular momentum exactly; their energy is a little lower than the one
        // orbit's. That is the grid, not heat, and it is kept apart.
        DiscResidualAngular += sense * mass * ((1 - share) * j0 + share * j1 - j);
        DiscResidualEnergy += gm * gm * mass * (1 / (2 * j * j) - (1 - share) / (2 * j0 * j0) - share / (2 * j1 * j1));
        return disc;
    }

    /// Heat Law, second representation for cold matter (SPEC §15.12). In belt `group` of `host` the `keep` heaviest
    /// nameless members stay objects; the small tail becomes one cold disc of the host. Each member's mass, matter and
    /// angular momentum about the host carry over exactly; the energy its orbit had above a circle is kept as the
    /// belt's heat; the host takes the tail's momentum, so the world's total does not move. Returns members folded.
    /// Not built yet: a folded belt does not hit planets, is not stirred by a passer-by, gives no parcel back.
    public int FoldBelt(int host, int group, int keep)
    {
        if (host < 0 || host >= N || !Alive[host] || group <= 0 || keep < 0) return 0;
        var members = new List<int>();
        for (int i = 0; i < N; i++)
            if (Alive[i] && i != host && Grp[i] == group && Name[i] == null && !Attracts(i)) members.Add(i);
        members.Sort((a, b) => M[a] != M[b] ? M[b].CompareTo(M[a]) : a.CompareTo(b));
        int ne = ElementCount, folded = 0;
        double gm = C.G * M[host], px = 0, py = 0, gone = 0;
        var matter = new double[ne];
        for (int n = keep; n < members.Count; n++)
        {
            int i = members[n];
            double dx = X[i] - X[host], dy = Y[i] - Y[host], vx = Vx[i] - Vx[host], vy = Vy[i] - Vy[host];
            double r = Math.Sqrt(dx * dx + dy * dy), m = M[i], spin = dx * vy - dy * vx;
            double energy = m * ((vx * vx + vy * vy) / 2 - (r > 0 ? gm / r : 0));
            if (!(r > 0) || !(energy < 0)) continue; // not held by this host: it stays an object
            for (int e = 0; e < ne; e++) matter[e] = Comp[i * ne + e];
            var disc = DepositDisc(host, matter, spin, cold: true);
            if (disc != null) disc.Heat += Math.Max(0, energy + gm * gm * m / (2 * spin * spin));
            px += m * vx; py += m * vy; gone += m;
            Kill(i); folded++;
        }
        if (gone > 0)
        {
            double carried = M[host] + DiscMassOn(host);
            Vx[host] += px / carried; Vy[host] += py / carried;
        }
        return folded;
    }

    void EvolveDiscs() { for (int n = _discs.Count - 1; n >= 0; n--) EvolveDisc(_discs[n]); }

    void EvolveDisc(Disc disc)
    {
        double seconds = (Year - disc.Year) * C.YearTime;
        if (!(seconds > 0)) return;
        disc.Year = Year;
        if (disc.Cold) return; // no gas, no friction: grinding and stirring of a belt are not built
        int host = disc.Host, ne = ElementCount;
        if (host < 0) return;
        double gm = C.G * M[host];
        if (!(gm > 0) || !(C.DiscViscousOrbits > 0)) return;
        if (DiscCellMass(disc, disc.Cells - 1) > 0) CoverDisc(disc, disc.Base, disc.Base + disc.Cells - 1 + DiscHeadroom);
        int cells = disc.Cells; double[] m = disc.Matter;
        if (_discGain.Length != ne) _discGain = new double[ne];
        double[] gain = _discGain; Array.Clear(gain);
        double sink = DiscSink(host), before = DiscEnergy(disc), angular = 0, energy = 0;

        // rings the host has grown over are swallowed whole
        int first = 0;
        for (; first < cells; first++)
        {
            double j = DiscSpin(disc.Base + first);
            if (j * j / gm > sink) break;
            for (int e = 0; e < ne; e++)
            {
                double c = m[first * ne + e];
                if (c == 0) continue;
                gain[e] += c; angular += c * j; energy -= gm * gm * c / (2 * j * j); m[first * ne + e] = 0;
            }
        }

        int n = cells - first;
        if (n > 1)
        {
            if (_discIn.Length < n) { _discIn = new double[n]; _discDen = new double[n]; _discCp = new double[n]; _discX = new double[n]; }
            double[] into = _discIn, den = _discDen, cp = _discCp, x = _discX;
            // share of a ring that steps inward during this step; the outermost ring only receives
            double rate = seconds / (Math.Tau * C.DiscViscousOrbits * 4 * DiscLnStep * DiscLnStep);
            for (int i = 0; i < n; i++)
            {
                double j = DiscSpin(disc.Base + first + i);
                into[i] = i == n - 1 ? 0 : Math.Min(rate * gm * gm / (j * j * j), DiscMaxHop);
            }
            // (1 + in + out) m[i] - in[i+1] m[i+1] - out[i-1] m[i-1] = old m[i], with out = in / DiscStep
            den[0] = 1 + into[0] + into[0] / DiscStep; cp[0] = -into[1] / den[0];
            for (int i = 1; i < n; i++)
            {
                double lower = -into[i - 1] / DiscStep;
                den[i] = 1 + into[i] + into[i] / DiscStep - lower * cp[i - 1];
                cp[i] = i < n - 1 ? -into[i + 1] / den[i] : 0;
            }
            double inner = DiscSpin(disc.Base + first - 1);
            for (int e = 0; e < ne; e++)
            {
                x[0] = m[first * ne + e] / den[0];
                for (int i = 1; i < n; i++) x[i] = (m[(first + i) * ne + e] + into[i - 1] / DiscStep * x[i - 1]) / den[i];
                for (int i = n - 2; i >= 0; i--) x[i] -= cp[i] * x[i + 1];
                for (int i = 0; i < n; i++) m[(first + i) * ne + e] = x[i];
                double fell = into[0] * x[0];
                gain[e] += fell; angular += fell * inner; energy -= gm * gm * fell / (2 * inner * inner);
            }
        }

        DiscRadiatedEnergy += before - DiscEnergy(disc) - energy;
        double total = 0, gained = 0;
        for (int i = 0; i < m.Length; i++) total += m[i];
        for (int e = 0; e < ne; e++) gained += gain[e];
        disc.Total = total;
        if (gained > 0) AccreteDisc(host, gain, disc.Sense * angular, energy);
        if (total > disc.Fed * 1e-9) return;

        // what is left is below anything a check or an eye can tell from nothing: the host takes it
        Array.Clear(gain); angular = 0; energy = 0; gm = C.G * M[host];
        for (int k = 0; k < cells; k++)
        {
            double j = DiscSpin(disc.Base + k);
            for (int e = 0; e < ne; e++) { double c = m[k * ne + e]; gain[e] += c; angular += c * j; energy -= gm * gm * c / (2 * j * j); }
        }
        AccreteDisc(host, gain, disc.Sense * angular, energy);
        disc.Total = 0; _discs.Remove(disc);
        LogEvent(host, "disc", "disc.spent", disc.Fed);
    }

    // The host is no more. What swallowed it inherits the disc; with no heir the disc stays where it was, frozen.
    void DiscsGone(int d, int heir)
    {
        foreach (var disc in _discs)
        {
            if (disc.Host != d) continue;
            disc.Host = heir; disc.HostGen = heir >= 0 ? Gen[heir] : 0;
        }
    }

    void HashDiscs(Action<ulong> mix)
    {
        bool dials = C.DiscOn != 1 || C.DiscViscousOrbits != 100 || C.DiscVaporEnergy != 0.0125 || C.DiscInnerOrbit != 3;
        // a world that never made a disc, with the dials untouched, hashes exactly as it did before this law
        if (!dials && _discs.Count == 0 && DiscAccretedMass == 0) return;
        void number(double v) => mix(BitConverter.DoubleToUInt64Bits(v));
        mix(0x44495343UL); // "DISC"
        number(C.DiscOn); number(C.DiscViscousOrbits); number(C.DiscVaporEnergy); number(C.DiscInnerOrbit);
        number(DiscRadiatedEnergy); number(DiscAccretedMass); number(DiscAccretedAngular); number(DiscAccretedEnergy);
        number(DiscResidualAngular); number(DiscResidualEnergy);
        mix((ulong)_discs.Count);
        foreach (var d in _discs)
        {
            mix((ulong)d.Host); mix((ulong)d.HostGen); mix((ulong)d.Sense); mix((ulong)d.Base); mix((ulong)d.Cells);
            number(d.Year); number(d.Fed);
            if (d.Cold) { mix(0x434F4C44UL); number(d.Heat); } // "COLD"; hot discs hash as before
            foreach (double c in d.Matter) number(c);
        }
    }
}
