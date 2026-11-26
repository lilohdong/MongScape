using System;

namespace Mongscape.Domain
{
    // Where a monkey was rescued (plan 3-0, 2-3a, 4-1): reaching out to the mine wall (Q/E on the keyboard counts the same),
    // a prison lock opening, the call item in the mine, or the sweat trait at the end of the run.
    public enum RescueSource { MineWall, PrisonLock, Call, Sweat }

    // Rescued monkeys of one run, the only currency (plan 4). They follow the runner as a troop: at most MaxVisible run
    // on screen, the rest are a number. At the end of the run the kept ones go to the account (the record's coins field).
    // The map workers call Rescue; the troop view reads Visible, Hidden and Version.
    public sealed class MonkeyTroop
    {
        public const int MaxVisible = 5;
        // Rescued this run, from any source.
        public int Rescued { get; private set; }
        // Taken by a chaser in place of a warning (loyalty trait).
        public int Lost { get; private set; }
        public int Kept => Math.Max(0, Rescued - Lost);
        public int Visible => Math.Min(MaxVisible, Kept);
        public int Hidden => Kept - Visible;
        public int FromSource(RescueSource source) => bySource[(int)source];
        // Changes every time the troop changes, so a view can react without events.
        public int Version { get; private set; }
        public RescueSource LastSource { get; private set; }
        // Whether the latest change took a monkey away (the view plays the leave instead of the join).
        public bool LastWasLoss { get; private set; }
        private readonly int[] bySource = new int[Enum.GetValues(typeof(RescueSource)).Length];

        public void Rescue(RescueSource source, int count = 1)
        {
            if (count <= 0) return;
            Rescued += count; bySource[(int)source] += count; LastSource = source; LastWasLoss = false; Version++;
        }
        // One following monkey is taken away. False when nobody follows.
        public bool LoseOne()
        {
            if (Kept <= 0) return false;
            Lost++; LastWasLoss = true; Version++; return true;
        }
        public void Reset() { Rescued = 0; Lost = 0; Array.Clear(bySource, 0, bySource.Length); LastWasLoss = false; Version++; }
    }
}
