using CombatExtended;
using CombatExtendedVek.Damage;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace CombatExtendedVek.Patch {
    public static class ProjectileCollision {
        //UnityEngine.Random.Range
        [HarmonyPatch(typeof(BulletCE), nameof(BulletCE.Impact), new Type[] { typeof(Thing) })]
        private static class BulletCE_Impact_RandomCollision {
            static bool Prefix(BulletCE __instance, Thing hitThing) {
                if (hitThing == null) {
                    __instance.ExactPosition =  new Vector3(
                        UnityEngine.Random.Range(__instance.ExactPosition.x, __instance.LastPos.x), 
                        UnityEngine.Random.Range(__instance.ExactPosition.y, __instance.LastPos.y),
                        UnityEngine.Random.Range(__instance.ExactPosition.z, __instance.LastPos.z));
                }
                return true;
            }
        }
    }
}
