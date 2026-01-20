using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using HarmonyLib;
using Stunl0ck.TLS.ModKit.DSL;
using TheLastStand.Database;
using TheLastStand.Definition.Item;
using TheLastStand.Definition.Unit; // BodyPartDefinition

namespace Stunl0ck.TLS.ModKit.Targets.Items
{
    internal sealed class ItemTargetAdapter : ITargetAdapter
    {
        public string TargetId => "ItemDefinition";
        public string DataFolderName => "Items";

        public void ApplyAdd(XElement definitionElement, string sourceFile, bool replace)
        {
            if (definitionElement == null) { Plugin.Log?.LogWarning($"[ModKit][Items] {sourceFile}: <Definition> null."); return; }

            ItemDefinition parsed;
            try
            {
                // Uses ItemDefinition(XContainer) which calls Deserialize in ctor flow
                parsed = new ItemDefinition(definitionElement);

                // Vanilla Deserialize() never reads <ArtId>/<BodyParts>; do it explicitly.
                parsed.DeserializeArtRelatedDatas(definitionElement);

                // If no <BodyParts> provided, clone from a donor that already uses the same ArtId.
                if (parsed.BodyPartsDefinitions == null || parsed.BodyPartsDefinitions.Count == 0)
                {
                    var art = parsed.ArtId;
                    if (!string.IsNullOrWhiteSpace(art))
                    {
                        var donor = ItemDatabase.ItemDefinitions?.Values
                            .FirstOrDefault(d => string.Equals(d?.ArtId, art, StringComparison.OrdinalIgnoreCase)
                                              && d?.BodyPartsDefinitions != null
                                              && d.BodyPartsDefinitions.Count > 0);
                        if (donor != null)
                        {
                            var cloned = new Dictionary<string, BodyPartDefinition>(donor.BodyPartsDefinitions);

                            // Try property setter first…
                            var setter = AccessTools.PropertySetter(typeof(ItemDefinition), nameof(ItemDefinition.BodyPartsDefinitions));
                            if (setter != null)
                            {
                                setter.Invoke(parsed, new object[] { cloned });
                            }
                            else
                            {
                                // …fallback to backing field (auto-prop name) or field name.
                                var f = AccessTools.Field(typeof(ItemDefinition), "<BodyPartsDefinitions>k__BackingField")
                                        ?? AccessTools.Field(typeof(ItemDefinition), "BodyPartsDefinitions");
                                f?.SetValue(parsed, cloned);
                            }

                            Plugin.Log?.LogInfo($"[ModKit][Items] '{parsed.Id}': BodyParts cloned from '{donor.Id}' (ArtId='{art}').");
                        }
                        else
                        {
                            Plugin.Log?.LogWarning($"[ModKit][Items] '{parsed.Id}': no donor with ArtId='{art}' found to clone BodyParts.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ModKit][Items] {sourceFile}: parse failed: {ex}");
                return;
            }

            var id = parsed?.Id;
            if (string.IsNullOrWhiteSpace(id)) { Plugin.Log?.LogWarning($"[ModKit][Items] {sourceFile}: no Id; skip."); return; }

            var map = ItemDatabase.ItemDefinitions;
            if (map.ContainsKey(id))
            {
                if (!replace) { Plugin.Log?.LogWarning($"[ModKit][Items] {sourceFile}: Id '{id}' exists. Use action='replace'."); return; }
                map[id] = parsed;
                Plugin.Log?.LogInfo($"[ModKit][Items] Replaced '{id}'.");
            }
            else
            {
                map.Add(id, parsed);
                Plugin.Log?.LogInfo($"[ModKit][Items] Added '{id}'.");
            }
        }

        public void ApplyEdit(string id, IReadOnlyList<PatchOperation> ops, string src)
            => Plugin.Log?.LogWarning($"[ModKit][Items] {src}: edit not implemented (skip).");

        public void ApplyRemove(string id, string src)
        {
            if (string.IsNullOrWhiteSpace(id)) { Plugin.Log?.LogWarning($"[ModKit][Items] {src}: remove needs id."); return; }
            if (ItemDatabase.ItemDefinitions.Remove(id))
                Plugin.Log?.LogInfo($"[ModKit][Items] Removed '{id}'.");
            else
                Plugin.Log?.LogInfo($"[ModKit][Items] Remove '{id}': not found (no-op).");
        }
    }
}
