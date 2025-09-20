using System;
using System.Linq;
using System.Reflection;

namespace Stunl0ck.TLS.ModKit
{
    internal static class McmShim
    {
        static bool _tried;
        static MethodInfo _getValueString;

        static void EnsureBound()
        {
            if (_tried) return;
            _tried = true;

            // Find loaded ModConfigManager assembly if present
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                         .FirstOrDefault(a => a.GetName().Name == "ModConfigManager");
            var t = asm?.GetType("Stunl0ck.ModConfigManager.MCM");
            var mi = t?.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static);
            _getValueString = mi?.MakeGenericMethod(typeof(string));
        }

        public static string GetString(string modId, string key, string fallback)
        {
            EnsureBound();
            if (_getValueString == null) return fallback;
            try { return (string)_getValueString.Invoke(null, new object[] { modId, key, fallback }); }
            catch { return fallback; }
        }
    }
}
