using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TheLastStand.Controller.Modding.Module;
using Stunl0ck.TLS.Shared;
using Stunl0ck.TLS.ModKit.Runtime;

namespace Stunl0ck.TLS.ModKit
{
    [BepInPlugin(ModId, ModName, Version)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string ModId = "com.tls.modkit";
        public const string ModName = "TLS ModKit";
        public const string Version = "1.0.0";

        public static ManualLogSource Log { get; private set; }
        private static bool s_initialized;

        private void Awake()
        {
            Log = Logger;

            if (s_initialized)
            {
                Log.LogInfo("[ModKit] Already initialized, skipping.");
                return;
            }
            s_initialized = true;

            // --- Load disk PNG icons from EVERY plugin folder under BepInEx/plugins/* ---
            int loadedTotal = LoadIconsFromAllMods();
            Log?.LogInfo($"[ModKit] Disk icon load complete. Total loaded: {loadedTotal}.");

            // --- Harmony patches ---
            Harmony.CreateAndPatchAll(typeof(Hooks.GlyphDatabaseHooks));   // DB-ready hook
            Harmony.CreateAndPatchAll(typeof(Hooks.GetGlyphIconHook));     // glyph icon override hook
            Harmony.CreateAndPatchAll(typeof(Hooks.PerkDatabaseHooks));   
            Harmony.CreateAndPatchAll(typeof(Hooks.PerkCtorHook)); 
            Harmony.CreateAndPatchAll(typeof(Hooks.LocalizationHooks));
            Harmony.CreateAndPatchAll(typeof(Hooks.LocalizerHooks));
            Harmony.CreateAndPatchAll(typeof(Hooks.ItemDatabaseHooks));
            Harmony.CreateAndPatchAll(typeof(Hooks.SkillDatabaseHooks));
            Harmony.CreateAndPatchAll(typeof(Hooks.BodyPartView_GetSprite_Hook));
            Harmony.CreateAndPatchAll(typeof(Hooks.GetUiSpriteHook));

            Log.LogInfo("[ModKit] Plugin initialized.");
        }
    }
}
