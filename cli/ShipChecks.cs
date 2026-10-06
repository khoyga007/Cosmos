using System;
using System.Collections.Generic;
using System.Linq;
using Cosmos.Core;

// Ships in stepping: how many of the ships a space-age Earth sends get where they were going.
static class ShipChecks
{
    const double H = 0.5;

    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string line) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} ships: {line}"); }

        foreach (bool colonies in new[] { false, true })
        {
            const int steps = 300000;
            var w = World.SolSystem(0, 1234);
            const int sun = 0, earth = 3;
            if (!colonies) w.Do(new Command(CmdKind.SetConst, Name: "ShipPop", Amount: 2)); // no ships during the jump
            w.Do(new Command(CmdKind.FastForward, Amount: colonies ? 7.5e5 : 6.95e5)); // just into the space age; later = the colonies send ships too
            w.Do(new Command(CmdKind.SetConst, Name: "ShipPop", Amount: 0.03));
            int civ = w.Civ[earth];

            // each ship from the step it appears to the step it is gone: where it was going and how close it came to the Sun
            var goal = new Dictionary<(int Slot, int Gen), int>();
            var nearest = new Dictionary<(int Slot, int Gen), double>();
            var perGoal = new SortedDictionary<int, (int Sent, int Lost)>();
            double worstNear = double.MaxValue, allNear = double.MaxValue, longest = 0; int lostBefore = 0, sent = 0, lost = 0, stuck = 0;
            for (int s = 0; s < steps; s++)
            {
                w.Advance(H);
                var up = new HashSet<(int, int)>();
                for (int i = 0; i < w.N; i++)
                {
                    if (!w.Alive[i] || !w.IsShip(i)) continue;
                    var id = (i, w.Gen[i]); up.Add(id);
                    double dx = w.X[i] - w.X[sun], dy = w.Y[i] - w.Y[sun], d = Math.Sqrt(dx * dx + dy * dy) / w.R[sun];
                    if (!goal.ContainsKey(id)) { goal[id] = w.ShipTo[i]; nearest[id] = d; sent++; }
                    else if (d < nearest[id]) nearest[id] = d;
                    allNear = Math.Min(allNear, d); longest = Math.Max(longest, w.Year - w.ShipBorn[i]);
                    if (d < 3) stuck++;
                }
                int lostNow = w.Chronicle.Count(c => c.Civ == civ && c.Event.Change == "civ.ship.lost");
                foreach (var id in goal.Keys.Where(k => !up.Contains(k)).ToList())
                {
                    bool gone = lostNow > lostBefore; // a loss was written in the step this ship went away
                    var g = perGoal.TryGetValue(goal[id], out var old) ? old : (0, 0);
                    perGoal[goal[id]] = (g.Item1 + 1, g.Item2 + (gone ? 1 : 0));
                    if (gone) { lost++; lostBefore++; worstNear = Math.Min(worstNear, nearest[id]); }
                    goal.Remove(id); nearest.Remove(id);
                }
                lostBefore = lostNow;
            }
            string table = string.Join(", ", perGoal.Select(p => $"{w.Name[p.Key]} {p.Value.Sent - p.Value.Lost}/{p.Value.Sent}"));
            Check(sent > 50 && lost <= sent / 50,
                $"{(colonies ? "five worlds sending" : "Earth alone")}: over {steps * H / w.C.YearTime:F0} years {sent} ships sent, {lost} lost ({(sent > 0 ? 100.0 * lost / sent : 0):F1}%); arrived per goal: {table}; a lost ship came within {(lost > 0 ? worstNear : 0):F2} Sun radii; any ship nearest {allNear:F2} Sun radii, ship-steps inside 3 radii {stuck}, longest flight {longest:F1} yr; Sun R {w.R[sun]:F2}, g at 1.5R {w.C.G * w.M[sun] / (2.25 * w.R[sun] * w.R[sun]):F2} vs thrust {w.C.ShipThrust}");
        }
        return ok;
    }
}
