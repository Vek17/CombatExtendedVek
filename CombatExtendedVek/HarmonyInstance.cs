using HarmonyLib;
using System.Reflection;

namespace CombatExtendedVek {
    public class HarmonyInstance {
        private static Harmony harmony = null;

        static internal Harmony instance {
            get {
                if (harmony == null) {
                    harmony = new Harmony("CombatExtendedVek.HarmonyCEV");
                }
                return harmony;
            }
        }

        public static void InitPatches() {
            instance.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
