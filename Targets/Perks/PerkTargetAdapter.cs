using Stunl0ck.TLS.ModKit.DSL;
using Stunl0ck.TLS.ModKit.Runtime;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Collections.Concurrent;
using TheLastStand.Database.Unit;
using TheLastStand.Definition.Unit.Perk;

namespace Stunl0ck.TLS.ModKit.Targets.Perks
{
    internal sealed class PerkTargetAdapter : ITargetAdapter
    {
        public string TargetId => "PerkDefinition";
        public string DataFolderName => "Perks";

        private static readonly ConcurrentQueue<(string id, string sourceFile)> _pendingRemoves =
            new ConcurrentQueue<(string id, string sourceFile)>();

        public void ApplyAdd(XElement definitionElement, string sourceFile, bool replace)
        {
            if (definitionElement == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: <Definition> was null.");
                return;
            }

            PerkDefinition parsed;
            try
            {
                ResolveMcmTokens(definitionElement);
                parsed = new PerkDefinition(definitionElement); // native XML ctor
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ModKit][Perks] {sourceFile}: failed to parse PerkDefinition XML: {ex}");
                return;
            }

            var id = parsed?.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: parsed perk has no Id; skipping.");
                return;
            }

            var map = PlayableUnitDatabase.PerkDefinitions;
            if (map == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: PerkDefinitions map is null; skipping.");
                return;
            }

            if (map.ContainsKey(id))
            {
                if (!replace)
                {
                    Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: Id '{id}' already exists. Use action='replace' to override.");
                    return;
                }

                map[id] = parsed;
                Plugin.Log?.LogInfo($"[ModKit][Perks] Replaced perk '{id}'.");
            }
            else
            {
                map.Add(id, parsed);
                Plugin.Log?.LogInfo($"[ModKit][Perks] Added perk '{id}'.");
            }
        }

        public void ApplyEdit(string id, IReadOnlyList<PatchOperation> operations, string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: edit requires a non-empty id.");
                return;
            }

            if (operations == null || operations.Count == 0)
            {
                Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: no operations provided for '{id}'.");
                return;
            }

            // Only accept token:Key selectors. Everything else is rejected.
            var filtered = new List<PatchOperation>(operations.Count);
            foreach (var op in operations)
            {
                if (op == null) continue;

                if (op.Kind != PatchOpKind.Set)
                {
                    Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: unsupported op '{op.Kind}' (only 'Set' is handled). Skipping.");
                    continue;
                }

                var sel = (op.Select ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(sel))
                {
                    Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: Set missing 'select'. Skipping.");
                    continue;
                }

                if (!sel.StartsWith("token:", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: select not supported (expected 'token:Key'): {sel}. Skipping.");
                    continue;
                }

                // Normalize to "token:Key" with trimmed key
                var key = sel.Substring("token:".Length).Trim();
                if (string.IsNullOrEmpty(key))
                {
                    Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: token: selector missing key. Skipping.");
                    continue;
                }

                // Keep the op; PerkCtorPatch will read op.Select ("token:Key") and op.Value
                filtered.Add(op);
            }

            if (filtered.Count == 0)
            {
                Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: no applicable edits for '{id}'.");
                return;
            }

            // Let PerkCtorPatch rewrite the XElement before the game parses it.
            PerkEditQueue.Enqueue(id, filtered);
            Plugin.Log?.LogInfo($"[ModKit][Perks] Queued {filtered.Count} edit(s) for '{id}' from {sourceFile} (will apply pre-ctor).");
        }

        public void ApplyRemove(string id, string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: remove requires a non-empty id.");
                return;
            }

            var map = PlayableUnitDatabase.PerkDefinitions;
            if (map == null)
            {
                _pendingRemoves.Enqueue((id, sourceFile));
                Plugin.Log?.LogWarning($"[ModKit][Perks] {sourceFile}: PerkDefinitions map is null; skipping remove '{id}'.");
                return;
            }

            if (map.Remove(id))
                Plugin.Log?.LogInfo($"[ModKit][Perks] Removed perk '{id}'.");
            else
                Plugin.Log?.LogInfo($"[ModKit][Perks] Remove '{id}': not found (no-op).");
        }

        internal static void DrainPendingRemoves()
        {
            var map = PlayableUnitDatabase.PerkDefinitions;
            if (map == null) return;

            int drained = 0;
            while (_pendingRemoves.TryDequeue(out var item))
            {
                if (string.IsNullOrWhiteSpace(item.id)) continue;

                if (map.Remove(item.id))
                    Plugin.Log?.LogInfo($"[ModKit][Perks] Removed perk '{item.id}' (drained).");
                else
                    Plugin.Log?.LogInfo($"[ModKit][Perks] Remove '{item.id}': not found (drained no-op).");

                drained++;
            }

            if (drained > 0)
                Plugin.Log?.LogInfo($"[ModKit][Perks] Drained {drained} pending remove(s).");
        }

        private static void ResolveMcmTokens(XElement elem)
        {
            foreach (var attr in elem.DescendantsAndSelf().Attributes())
            {
                var s = attr.Value?.Trim();
                if (s != null && s.StartsWith("${MCM:", StringComparison.Ordinal))
                {
                    var payload = s.Substring(6, s.Length - 7); // inside ${MCM:...}
                    var slash   = payload.IndexOf('/');
                    var modId   = slash > 0 ? payload.Substring(0, slash) : payload;
                    var key     = slash > 0 ? payload.Substring(slash + 1) : "";

                    var val = Stunl0ck.TLS.ModKit.McmShim.GetString(modId, key, "");
                    attr.Value = val ?? "";
                }
            }
        }
    }
}
