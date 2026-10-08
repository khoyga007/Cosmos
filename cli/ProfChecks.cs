using System;
using System.Linq;
using Cosmos.Core;

static class ProfChecks
{
    public static bool Run()
    {
        bool ok = true;
        void Check(bool pass, string message) { ok &= pass; Console.WriteLine($"{(pass ? "OK    " : "FAILED")} prof: {message}"); }

        var analytical = new World(8, 1234);
        foreach (var rule in analytical.Rules) analytical.Do(new Command(CmdKind.SetRule, Name: rule.Id, Amount: 0));
        analytical.Add(-1000, 0, 0, 0, 2e-4, analytical.Mix(("rock", 1)));
        analytical.Add(1000, 0, 0, 0, 3e-4, analytical.Mix(("rock", 1)));
        analytical.Add(10000, 0, 0, 0, 1e-9, analytical.Mix(("rock", 1)));
        analytical.ResetProf();
        analytical.Advance(.5); analytical.Advance(.5);
        var expected = new WorkCounters(2, 16, 64, 16, 0, 0, 0, 0, 0);
        Check(analytical.Prof == expected, $"two pullers + one far rock: {analytical.Prof}; expected {expected}");
        ulong hash = analytical.Hash();
        _ = analytical.Prof; _ = analytical.RuleApplications("missing"); analytical.ResetProf();
        Check(analytical.Hash() == hash && analytical.Prof == default, "read/reset leaves canonical hash unchanged and clears diagnostic counts");
        try { analytical.Advance(double.NaN); Check(false, "invalid Advance must throw"); }
        catch (ArgumentOutOfRangeException) { Check(analytical.Prof == default && analytical.Hash() == hash, "rejected Advance does not count or change physics"); }

        var contact = new World(4, 23);
        foreach (var rule in contact.Rules) contact.Do(new Command(CmdKind.SetRule, Name: rule.Id, Amount: 0));
        contact.Add(0, 0, 0, 0, 2e-4, contact.Mix(("rock", 1)));
        contact.Add(-1, 0, 40, 0, 1e-9, contact.Mix(("rock", 1)));
        contact.ResetProf(); contact.Advance(.1);
        Check(contact.Prof.ContactSweeps > 0 && contact.Prof.Merges == 1 && contact.Merges == 1,
            $"real swept contact and merge counted: {contact.Prof}");
        hash = contact.Hash(); contact.ResetProf();
        Check(contact.Prof.Merges == 0 && contact.Merges == 1 && contact.Hash() == hash, "merge reset is diagnostic delta, not a physics reset");

        World Scene(int threads)
        {
            var w = World.SolSystem(5000, 77); w.Threads = threads;
            w.ResetProf();
            w.Do(new Command(CmdKind.Push, Target: 3, Vx: .01, Vy: -.02));
            for (int i = 0; i < 40; i++) w.Advance(.5);
            return w;
        }
        var serial = Scene(1);
        foreach (int threads in new[] { 4, 8 })
        {
            var parallel = Scene(threads);
            Check(parallel.Hash() == serial.Hash() && parallel.Prof == serial.Prof
                && serial.Rules.All(r => serial.RuleApplications(r.Id) == parallel.RuleApplications(r.Id)),
                $"Threads1/{threads}: hash {serial.Hash():X16}/{parallel.Hash():X16}, exact work {parallel.Prof}");
        }
        var replay = World.SolSystem(5000, 77); replay.Threads = 4; replay.ResetProf(); int next = 0;
        while (replay.Step < serial.Step) { replay.Replay(serial.Journal, ref next); replay.Advance(.5); }
        replay.Replay(serial.Journal, ref next);
        Check(replay.Hash() == serial.Hash() && replay.Prof == serial.Prof, "journal replay reproduces hash and all phase counts");
        Check(serial.Prof.RocheCollections == 40 && serial.Prof.RocheAdmissionPairs > 0
            && serial.Prof.RuleApplications == serial.Rules.Sum(r => serial.RuleApplications(r.Id))
            && serial.RuleApplications("temperature") > 0, "Roche collection count and actual due-rule calls are accounted");
        return ok;
    }
}
