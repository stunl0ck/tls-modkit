using HarmonyLib;
using TheLastStand.Database.Unit;
using Stunl0ck.TLS.ModKit.Targets.Perks;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    // Run the ModKit perk patch pass *before* the game constructs PerkDefinitions.
    internal static class PerkDatabaseHooks
    {
        private static bool _appliedOnce;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayableUnitDatabase), "DeserializePerks")]
        private static void Prefix()
        {
            if (_appliedOnce) return;          // only once per session
            _appliedOnce = true;

            // At this moment, the game is about to parse the perk XML and call the ctor.
            // We enqueue our edits now so PerkCtorPatch can apply them pre-ctor.
            try
            {
                Plugin.Log?.LogInfo("[ModKit] Perk DB about to build. Queuing perk edits…");
                PatchEngine.Apply("PerkDefinition");
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogError($"[ModKit] Perk patching failed (pre-ctor queue): {ex}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayableUnitDatabase), "DeserializePerks")]
        private static void Postfix()
        {
            // Now the map exists; drain queued removes.
            PerkTargetAdapter.DrainPendingRemoves();
        }

    }
}
