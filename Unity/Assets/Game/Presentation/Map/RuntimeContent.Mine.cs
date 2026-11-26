using System.Collections.Generic;
using Mongscape.Domain;
using UnityEngine;

namespace Mongscape.Presentation.Map
{
    // Mine walks: the stuck monkeys of MineRescuePlan, pooled, and their reaction to the run's rescue judge.
    public sealed partial class RuntimeContent
    {
        private readonly Stack<StuckMonkeyView> stuckPool = new Stack<StuckMonkeyView>();
        private readonly List<StuckMonkeyView> stuckActive = new List<StuckMonkeyView>(4);
        private RunSession session;
        // The run whose rescue judge moves the monkeys (set once by the bootstrap).
        public RunSession Session
        {
            get => session;
            set
            {
                if (session != null) session.MineRescue.RescuedAt -= OnRescued;
                session = value;
                if (session != null) session.MineRescue.RescuedAt += OnRescued;
            }
        }
        public IReadOnlyList<StuckMonkeyView> StuckMonkeys => stuckActive;

        private void PlaceStuckMonkey(MongscapeMapChunk chunk, int sequence, List<Component> list)
        {
            int side = chunk.Layout.RescueSide;
            if (side == 0) return;
            var view = stuckPool.Count > 0 ? stuckPool.Pop() : StuckMonkeyView.Build(MineArt.Load());
            float z = MineRescuePlan.RouteZ(sequence);
            RouteSurface.Sample(z, out float x, out float y);
            view.transform.SetParent(chunk.transform, true);
            view.Place(new Vector3(x, y, z), side, sequence);
            list.Add(view); stuckActive.Add(view);
        }
        private bool ReleaseStuckMonkey(Component item)
        {
            if (!(item is StuckMonkeyView view)) return false;
            view.gameObject.SetActive(false); view.transform.SetParent(root, true);
            stuckActive.Remove(view); stuckPool.Push(view);
            return true;
        }
        private void OnRescued(int sequence, int side)
        {
            foreach (var view in stuckActive) if (view.Sequence == sequence) { view.Rescue(); return; }
        }
    }
}
