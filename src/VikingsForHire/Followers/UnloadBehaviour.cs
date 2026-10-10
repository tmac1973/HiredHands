using System.Collections.Generic;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Hirelings.Work;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// The Command Stone pointed at a chest: walk to it (to a spot within reach, as deliveries do) and put in what it
    /// already holds from the cargo, then drop the order, so the follower goes back to following or to its spot.
    /// </summary>
    internal sealed class UnloadBehaviour : IHirelingBehaviour
    {
        private const float GiveUpSeconds = 20f;

        private readonly Hirelings.Work.Chores.WorkSteps _walk = new();
        private Container? _walkTo;

        public string Name => "Unload";
        public int Priority => 450; // above following, below fighting and fleeing

        public bool Wants(HirelingAI ai)
        {
            if (ai.Order is not { Kind: FieldOrder.OrderKind.Unload } order)
                return false;
            if (order.Expired || order.Target is not Container chest || chest == null || ai.Hireling.CargoInventory == null)
            {
                Done(ai, "expired");
                return false;
            }
            return true;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            var chest = (Container)ai.Order!.Target!;
            h.SetActivity("$vfh_status_delivering");
            if (_walkTo != chest)
            {
                _walkTo = chest;
                _walk.Reset();
            }
            if (!_walk.Approach(ai, dt, chest, chest.transform.position))
            {
                if (_walk.SinceProgress > GiveUpSeconds)
                    Done(ai, "unreachable");
                return;
            }
            ai.Halt();
            int moved = 0, left = 0;
            foreach (KeyValuePair<string, int> item in FieldOrder.ForChest(h.CargoInventory!, chest))
            {
                int n = ContainerAccess.Deposit(chest, h.CargoInventory!, item.Key, item.Value, h.Hid);
                moved += n;
                left += item.Value - n;
            }
            VfhLog.I(LogCat.Orders, "order.unloaded", ("hid", h.Hid), ("chest", chest.transform.position), ("moved", moved), ("didntFit", left));
            Done(ai, left > 0 ? "chest full" : "done");
        }

        private void Done(HirelingAI ai, string why)
        {
            if (ai.Order is { Kind: FieldOrder.OrderKind.Unload })
            {
                VfhLog.D(LogCat.Orders, "order.done", ("hid", ai.Hireling.Hid), ("kind", FieldOrder.OrderKind.Unload), ("why", why));
                ai.Order = null;
            }
            _walkTo = null;
        }
    }
}
