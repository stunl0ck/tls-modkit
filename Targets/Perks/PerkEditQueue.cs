using System;
using System.Collections.Generic;

namespace Stunl0ck.TLS.ModKit.Targets.Perks
{
    /// <summary>
    /// Simple in-memory queue: PerkId -> list of Set operations to apply pre-ctor.
    /// (The apply pass enqueues; the ctor prefix consumes and removes.)
    /// </summary>
    internal static class PerkEditQueue
    {
        private static readonly Dictionary<string, List<DSL.PatchOperation>> _byId =
            new Dictionary<string, List<DSL.PatchOperation>>(StringComparer.OrdinalIgnoreCase);

        public static void Enqueue(string id, IReadOnlyList<DSL.PatchOperation> ops)
        {
            if (string.IsNullOrWhiteSpace(id) || ops == null || ops.Count == 0) return;

            if (!_byId.TryGetValue(id, out var list))
            {
                list = new List<DSL.PatchOperation>(ops.Count);
                _byId[id] = list;
            }
            list.AddRange(ops);
        }

        public static bool TryDequeue(string id, out List<DSL.PatchOperation> ops)
        {
            if (id != null && _byId.TryGetValue(id, out ops))
                return _byId.Remove(id);

            ops = null;
            return false;
        }
    }
}
