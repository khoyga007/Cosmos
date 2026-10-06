// SPEC S1: a jump is cut into slices for the rule table's sake. The cutting is an implementation detail and must
// not reach the journal: an event that happens inside a slice is stamped with the year it happened, not with the
// year the slice ended. And a star's own boundary is an instant, so everything it does lands on that instant.
using System;
using System.Globalization;
using System.Linq;
using Cosmos.Core;

static class StampChecks
{
    static int Jump(World w, double years) => w.Do(new Command(CmdKind.FastForward, Amount: years));

    static World Cut(params double[] parts)
    {
        var w = World.SolSystem(0, 1234);
        foreach (double p in parts) Jump(w, p);
        return w;
    }

    // The star clock itself drifts by ~3e-4 years between cuttings (the year goes through year time and back);
    // every real defect here is millions of years. A slice-end stamp cannot hide under this.
    const double Tick = 1e-2;

    static bool StarTick(RuleEvent e) => e.RuleId is "stars" or "temperature" or "water"
        || e.Change is "merge" or "life.end" or "civ.end";

    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} stamp: {line}"); }
        void Band(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} band: {line}"); }
        RuleEvent[] Star(World w) => w.Events.Where(StarTick).ToArray();

        // ---- one span, five cuttings: the same 1.01e10 years in 1, 2, 5 and 10 calls
        var runs = new[]
        {
            Cut(1.01e10),
            Cut(1e10, 1e8),
            Cut(5e9, 5.1e9),
            Cut(9e9, 1.1e9),
            Cut(1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9),
        };
        var head = Star(runs[0]);
        bool same = runs.All(w => Star(w).Length == head.Length);
        double worst = 0;
        string worstAt = "-";
        if (same)
            foreach (var w in runs)
            {
                var got = Star(w);
                for (int i = 0; i < head.Length; i++)
                {
                    if (head[i].ObjectSlot != got[i].ObjectSlot || head[i].Change != got[i].Change || head[i].RuleId != got[i].RuleId)
                    { same = false; worstAt = $"{head[i].ObjectSlot}:{head[i].Change} vs {got[i].ObjectSlot}:{got[i].Change}"; goto done; }
                    double gap = Math.Abs(head[i].Year - got[i].Year);
                    if (gap > worst) { worst = gap; worstAt = $"{head[i].ObjectSlot}:{head[i].Change}"; }
                }
            }
        done:
        Check(same && worst < Tick, $"{head.Length} star-tick events come out the same however the 1.01e10 year jump is cut "
            + $"(1, 2, 5 and 10 calls; worst year gap {worst:E3} yr at {worstAt})");

        // ---- the giant's instant, and the same instant when the jump does not stop there
        var at = Cut(1e10);
        var past = Cut(1e10, 1e6);
        var giant = at.Events.First(e => e.Change == "star.giant");
        var giantPast = past.Events.First(e => e.Change == "star.giant");
        var wake = Star(at);
        var wakePast = Star(past);
        Check(wake.Length == wakePast.Length && wake.Zip(wakePast).All(p => p.First.Change == p.Second.Change
                && p.First.ObjectSlot == p.Second.ObjectSlot && Math.Abs(p.First.Year - p.Second.Year) < Tick),
            $"stopping at the giant and running 1e6 years past it give the same {wake.Length} star-tick events at the same years: "
            + $"the wake is the giant's, {giantPast.Year - giant.Year + 1e6:E3} years before the longer jump ends");
        var tickLines = wake.Count(e => Math.Abs(e.Year - giant.Year) < Tick);
        Check(tickLines >= 16 && wake.All(e => Math.Abs(e.Year - giant.Year) < Tick),
            $"the giant's instant is one tick of {tickLines - 1} rule lines, all sharing its year {giant.Year:G17}; "
            + $"the jump end {at.Year:G17} adds nothing of its own: the rules run on the boundary, not after it");

        // ---- the engulfment: Mercury is inside the envelope the instant the envelope exists
        var merge = at.Events.First(e => e.Change == "merge" && e.ObjectSlot == 1);
        Check(merge.Year == giant.Year && !at.Alive[1],
            $"Mercury's engulfment is stamped {merge.Year:G17}, the giant's own year, not the end {at.Year:G17} of the slice that noticed it");

        // ---- the death is an instant of its own, half a billion years later
        var dead = Cut(1.1e10);
        var remnant = dead.Events.First(e => e.Change == "star.remnant.white");
        var mergeDead = dead.Events.First(e => e.Change == "merge" && e.ObjectSlot == 1);
        double naive = 1e10 * (1 + dead.C.StarGiantFraction); // the Sun's own life (StarLifetime(50) = 1e10 yr) plus the giant's share
        Check(dead.StarPhaseOf(0) == StarPhase.WhiteDwarf && remnant.Year > mergeDead.Year + 4e8
            && Math.Abs(remnant.Year - naive) < 1e4,
            $"the death carries its own stamp: engulfment {mergeDead.Year:G17}, remnant at {remnant.Year:G17}, "
            + $"{remnant.Year - mergeDead.Year:E3} years apart, {remnant.Year - naive:F1} yr off the naive {naive:G17} boundary");

        // ---- (a) one step down: scorched -> frozen, and the temperate middle never a state of the world
        var bands = dead.Events.Where(e => e.RuleId == "temperature").ToArray();
        var atDeath = bands.Where(e => Math.Abs(e.Year - remnant.Year) < Tick).ToArray();
        int[] planets = { 2, 3, 4, 5, 6, 7, 8 };
        Band(planets.All(i => atDeath.Count(e => e.ObjectSlot == i) == 1 && atDeath.Single(e => e.ObjectSlot == i).Change == "band.2.0")
            && atDeath.All(e => e.A > dead.C.ScorchedEdge && e.B < dead.C.FrozenEdge),
            $"{atDeath.Length} worlds fall scorched -> frozen in one line each at the death, {atDeath.Min(e => e.A):F0}K -> "
            + $"{atDeath.Max(e => e.B):F0}K: the temperate band {dead.C.FrozenEdge:F0}..{dead.C.ScorchedEdge:F0}K is stepped over, never lived in");
        Band(bands.Where(e => e.Year > mergeDead.Year + 1).All(e => e.Change == "band.2.0"),
            "after the giant the only band move left in the whole run is into band 0: nothing is ever parked in the middle");
        var waited = Cut(2e10).Events.Count(e => e.RuleId == "temperature" && e.ObjectSlot == 3);
        Band(waited == 2,
            $"Earth logs {waited} band changes in 2e10 years "
            + "(up at the giant, down at the death): the middle is a step, not a thaw that waiting would fill in");

        // ---- measured, not asserted: the cutting still reaches the colony rule in core/Civ.cs (Claire's file, untouched here)
        var colonyA = Cut(1.01e10).Events.Where(e => e.Change == "civ.colony").Select(e => $"{e.ObjectSlot}@{e.Year.ToString("G6", CultureInfo.InvariantCulture)}");
        var colonyB = Cut(1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9, 1.01e9)
            .Events.Where(e => e.Change == "civ.colony").Select(e => $"{e.ObjectSlot}@{e.Year.ToString("G6", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"      note: outside this spec -- the same colonies are founded either way but dated differently by the cutting: "
            + $"{string.Join(", ", colonyA)} vs {string.Join(", ", colonyB)}");
        return ok;
    }
}
