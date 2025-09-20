using HarmonyLib;
using TheLastStand.Controller.Modding.Module; // for LocalizationModuleController
using BepInEx;
using System;
using TPLib.Localization;
using Stunl0ck.TLS.Shared;

namespace Stunl0ck.TLS.ModKit.Hooks
{
    internal static class LocalizationHooks
    {
        // guard to avoid reinjecting on scene reloads (LoadLanguages can be called on language switch)
        private static bool _injectedOnce;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LocalizationModuleController), "LoadLanguages")]
        private static void InjectTranslations_ModKit()
        {
            if (_injectedOnce) return;
            _injectedOnce = true;

            var log = new Localization.Logger(
                info:  s => Plugin.Log?.LogInfo(s),
                warn:  s => Plugin.Log?.LogWarning(s),
                error: s => Plugin.Log?.LogError(s));

            // Pick up BepInEx/plugins/*/ModKit/languages.csv
            Localization.MergeCsvsUnder(Paths.PluginPath, "ModKit/languages.csv", log);
        }
    }
}
