using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using TheLastStand.Manager;
using TheLastStand.Manager.Item;
using TPLib;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    internal static class CityStashHooks
    {
        private const string ModKitFolderName = "ModKit";
        private const string CategoryFolderName = "CityStash";
        private const string PatchGlob = "*.patch.xml";

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "Start")]
        private static void GameManager_Start_Postfix()
        {
            try
            {
                var state = ApplicationManager.Application.State?.GetName() ?? string.Empty;
                if (!string.Equals(state, "NewGame", StringComparison.Ordinal))
                    return;

                // Avoid per-run noise if nobody is using this feature.
                if (!HasAnyCityStashPatches())
                    return;

                var invCtl = TPSingleton<InventoryManager>.Instance?.Inventory?.InventoryController;
                if (invCtl == null)
                {
                    Plugin.Log?.LogWarning("[ModKit][CityStash] InventoryController not ready; skipping stash patches.");
                    return;
                }

                Runtime.CityStashRuntime.CurrentInventoryController = invCtl;
                PatchEngine.Apply("CityStash");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ModKit][CityStash] Failed to apply stash patches: {ex}");
            }
            finally
            {
                Runtime.CityStashRuntime.CurrentInventoryController = null;
            }
        }

        private static bool HasAnyCityStashPatches()
        {
            var pluginsRoot = Paths.PluginPath;
            if (string.IsNullOrWhiteSpace(pluginsRoot) || !Directory.Exists(pluginsRoot))
                return false;

            try
            {
                foreach (var pluginDir in Directory.EnumerateDirectories(pluginsRoot))
                {
                    var catDir = Path.Combine(pluginDir, ModKitFolderName, CategoryFolderName);
                    if (!Directory.Exists(catDir))
                        continue;

                    foreach (var _ in Directory.EnumerateFiles(catDir, PatchGlob, SearchOption.AllDirectories))
                        return true;
                }
            }
            catch
            {
                // ignore IO errors
            }

            return false;
        }
    }
}

