// Items hook: run after vanilla items are loaded (after skills)
using HarmonyLib;
using System.Xml.Linq;
using TheLastStand.Database;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    internal static class ItemDatabaseHooks
    {
        static bool _appliedOnce;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemDatabase), nameof(ItemDatabase.Deserialize), new[] { typeof(XContainer) })]
        static void Postfix()
        {
            if (_appliedOnce) return;
            _appliedOnce = true;

            if (ItemDatabase.ItemDefinitions == null)
            {
                Plugin.Log?.LogWarning("[ModKit] ItemDefinitions is null; skipping item patches.");
                return;
            }

            Plugin.Log?.LogInfo($"[ModKit] Items ready ({ItemDatabase.ItemDefinitions.Count}). Applying item patches…");
            PatchEngine.Apply("ItemDefinition");
            Plugin.Log?.LogInfo($"[ModKit] Item patch pass done. Total: {ItemDatabase.ItemDefinitions.Count}.");
        }
    }
}
