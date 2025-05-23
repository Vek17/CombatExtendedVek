using HarmonyLib;
using System.Reflection;
using Verse;

namespace CombatExtendedVek {
    [StaticConstructorOnStartup]
    public static class HarmonyInstance {
        private static Harmony harmony = null;

        static internal Harmony instance {
            get {
                if (harmony == null) {
                    harmony = new Harmony("CombatExtendedVek.HarmonyCEV");
                }
                return harmony;
            }
        }

        static HarmonyInstance() {
            instance.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
