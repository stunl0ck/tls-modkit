using HarmonyLib;
using System;
using System.Linq;
using System.Xml.Linq;
using TheLastStand.Definition.Unit.Perk;
using Stunl0ck.TLS.ModKit.Targets.Perks;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    /// <summary>
    /// Harmony prefix that runs before PerkDefinition is constructed from XML.
    /// Mutate the XElement in-place based on queued edit operations.
    /// </summary>
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch]
    internal static class PerkCtorHook
    {
        // Target the single-parameter ctor: PerkDefinition(XElement)
        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase Target()
        {
            return AccessTools.Constructor(typeof(PerkDefinition), new[] { typeof(XElement) });
        }

        [HarmonyPrefix]
        private static void Prefix(ref XElement container)
        {
            if (container == null) return;
            if (!string.Equals(container.Name.LocalName, "PerkDefinition", StringComparison.OrdinalIgnoreCase))
                return;

            var idAttr = container.Attribute("Id");
            var id = idAttr?.Value?.Trim();
            if (string.IsNullOrEmpty(id)) return;

            if (!PerkEditQueue.TryDequeue(id, out var ops) || ops == null || ops.Count == 0)
                return;

            int applied = 0;
            foreach (var op in ops)
            {
                if (op == null || op.Kind != DSL.PatchOpKind.Set) continue;

                var sel = (op.Select ?? string.Empty).Trim();
                var val = op.Value ?? string.Empty;

                // token:Key
                if (sel.StartsWith("token:", StringComparison.OrdinalIgnoreCase))
                {
                    var key = sel.Substring("token:".Length).Trim();
                    if (!string.IsNullOrEmpty(key) && ApplyTokenSet(container, key, val))
                        applied++;
                    continue;
                }
            }

            if (applied > 0)
                Plugin.Log?.LogInfo($"[ModKit][Perks] Applied {applied} pre-ctor edit(s) to '{id}'.");
        }

        // ----- helpers -----
        private static bool ApplyTokenSet(XElement perkRoot, string key, string value)
        {
            // Ensure <TokenVariables> exists
            var tokenVars = perkRoot.Element("TokenVariables");
            if (tokenVars == null)
            {
                tokenVars = new XElement("TokenVariables");
                var first = perkRoot.Elements().FirstOrDefault();
                if (first != null) first.AddBeforeSelf(tokenVars);
                else perkRoot.Add(tokenVars);
            }

            // Find or create <TokenVariable Key="...">
            var node = tokenVars.Elements("TokenVariable")
                                .FirstOrDefault(e =>
                                    string.Equals((string)e.Attribute("Key"), key, StringComparison.Ordinal));

            if (node == null)
            {
                node = new XElement("TokenVariable",
                    new XAttribute("Key", key),
                    new XAttribute("Value", value));
                tokenVars.Add(node);
                return true;
            }

            var valAttr = node.Attribute("Value");
            if (valAttr == null) node.Add(new XAttribute("Value", value));
            else valAttr.Value = value;

            return true;
        }
    }
}
