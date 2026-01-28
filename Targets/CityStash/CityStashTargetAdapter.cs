using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using Stunl0ck.TLS.ModKit.DSL;
using Stunl0ck.TLS.ModKit.Runtime;
using TheLastStand.Controller.Item;
using TheLastStand.Database;
using TheLastStand.Definition.Item;
using TheLastStand.Manager.Item;

namespace Stunl0ck.TLS.ModKit.Targets.CityStash
{
    internal sealed class CityStashTargetAdapter : ITargetAdapter
    {
        public string TargetId => "CityStash";
        public string DataFolderName => "CityStash";

        public void ApplyAdd(XElement definitionElement, string sourceFile, bool replace)
        {
            if (definitionElement == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: <Definition> was null.");
                return;
            }

            var invCtl = CityStashRuntime.CurrentInventoryController;
            if (invCtl == null)
            {
                Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: InventoryController not available; skipping.");
                return;
            }

            // Support either:
            //  - <Definition><Item .../></Definition>
            //  - <Definition><CityStash><Item .../></CityStash></Definition>
            IEnumerable<XElement> items;
            if (string.Equals(definitionElement.Name.LocalName, "Item", StringComparison.OrdinalIgnoreCase))
                items = new[] { definitionElement };
            else
                items = definitionElement.Descendants("Item");

            int added = 0;
            foreach (var itemElem in items)
            {
                if (TryAddOne(invCtl, itemElem, sourceFile))
                    added++;
            }

            if (added == 0)
                Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: no valid <Item .../> entries found.");
        }

        public void ApplyEdit(string id, IReadOnlyList<PatchOperation> operations, string sourceFile)
            => Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: edit not supported (use action='add').");

        public void ApplyRemove(string id, string sourceFile)
            => Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: remove not supported.");

        private static bool TryAddOne(InventoryController invCtl, XElement itemElem, string sourceFile)
        {
            try
            {
                // If the author provided an Enabled attr, treat inability to parse as disabled.
                // If omitted, default to enabled.
                bool enabled = true;
                if (itemElem.Attribute("Enabled") != null)
                    enabled = ReadBool(itemElem, "Enabled", fallback: false);

                if (!enabled) return false;

                var id = ResolveMcm((string)itemElem.Attribute("Id"));
                if (string.IsNullOrWhiteSpace(id))
                {
                    Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: <Item> missing Id; skipping.");
                    return false;
                }

                int qty = ReadInt(itemElem, "Qty", 1);
                if (qty <= 0)
                    return false;

                int initLevel = ReadInt(itemElem, "Level", 0);
                var rarity = ReadRarity(itemElem, "Rarity", ItemDefinition.E_Rarity.Common);

                if (!ItemDatabase.ItemDefinitions.TryGetValue(id, out var def))
                {
                    Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: ItemDefinition '{id}' not found; skipping.");
                    return false;
                }

                var lvl = def.GetHigherExistingLevelFromInitValue(initLevel);
                if (lvl < 0)
                    lvl = def.GetLowerExistingLevelFromInitValue(initLevel);

                if (lvl < 0)
                {
                    Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: ItemDefinition '{id}' has no valid levels; skipping.");
                    return false;
                }

                for (int i = 0; i < qty; i++)
                {
                    var gen = new ItemManager.ItemGenerationInfo
                    {
                        ItemDefinition = def,
                        Level = lvl,
                        Rarity = rarity,
                        Destination = ItemSlotDefinition.E_ItemSlotId.Inventory,
                        SkipMalusAffixes = true
                    };

                    var item = ItemManager.GenerateItem(gen);
                    invCtl.AddItem(item, null, false);
                }

                Plugin.Log?.LogInfo($"[ModKit][CityStash] Added {qty}x '{id}' to city stash (lvl={lvl}, rarity={rarity}).");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[ModKit][CityStash] {sourceFile}: failed to add item: {ex.Message}");
                return false;
            }
        }

        private static int ReadInt(XElement elem, string attr, int fallback)
        {
            var raw = ResolveMcm((string)elem.Attribute(attr));
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        private static ItemDefinition.E_Rarity ReadRarity(XElement elem, string attr, ItemDefinition.E_Rarity fallback)
        {
            var raw = ResolveMcm((string)elem.Attribute(attr))?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            return Enum.TryParse(raw, ignoreCase: true, out ItemDefinition.E_Rarity r) ? r : fallback;
        }

        private static bool ReadBool(XElement elem, string attr, bool fallback)
        {
            var raw = ResolveMcm((string)elem.Attribute(attr))?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            if (bool.TryParse(raw, out var b))
                return b;
            if (raw == "0")
                return false;
            if (raw == "1")
                return true;

            return fallback;
        }

        private static string ResolveMcm(string raw)
        {
            var s = raw?.Trim();
            if (string.IsNullOrEmpty(s))
                return raw;

            if (!s.StartsWith("${MCM:", StringComparison.Ordinal) || !s.EndsWith("}", StringComparison.Ordinal))
                return raw;

            var payload = s.Substring(6, s.Length - 7); // inside ${MCM:...}
            var slash = payload.IndexOf('/');
            var modId = slash > 0 ? payload.Substring(0, slash) : payload;
            var key = slash > 0 ? payload.Substring(slash + 1) : "";

            return Stunl0ck.TLS.ModKit.McmShim.GetString(modId, key, "");
        }
    }
}
