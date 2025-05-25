using CombatExtended;
using CombatExtended.AI;
using CombatExtendedVek.Comps;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace CombatExtendedVek.Verbs {
    public class Verb_ShootCEV : Verb_ShootCE {
        public CompHyperburst? compHyperburst = null;
        private int hyperburstStartTick = 0;

        public virtual CompHyperburst? CompHyperburst {
            get {
                if (compHyperburst == null && EquipmentSource != null) {
                    compHyperburst = EquipmentSource.TryGetComp<CompHyperburst>();
                }
                return compHyperburst;
            }
        }

        public override void ShiftTarget(ShiftVecReport report, Vector3 v, bool calculateMechanicalOnly = false, bool isInstant = false) {
            if (CompHyperburst == null) {
                base.ShiftTarget(report, v, calculateMechanicalOnly, isInstant);
                return;
            }
            if (CompFireModes == null || CompFireModes.CurrentFireMode == FireMode.SingleFire || (CompFireModes.CurrentFireMode == FireMode.AutoFire && !CompHyperburst.ApplyDuringAuto)) {
                base.ShiftTarget(report, v, calculateMechanicalOnly, isInstant);
                return;
            }
            if (!calculateMechanicalOnly) {
                Vector3 u = caster.TrueCenter();
                sourceLoc.Set(u.x, u.z);

                if (numShotsFired == 0) {
                    // On first shot of burst do a range estimate
                    estimatedTargDist = report.GetRandDist();
                }


                if (report.targetPawn != null) {
                    v += report.targetPawn.Drawer.leaner.LeanOffset * 0.5f;
                }
                if (numShotsFired == 0) {
                    hyperburstStartTick = Find.TickManager.TicksAbs;
                }
                // Do not look for target movement if mid hyperburst
                if (numShotsFired > CompHyperburst.HyperburstShotCount || numShotsFired == 0) {
                    newTargetLoc.Set(v.x, v.z);

                    // ----------------------------------- STEP 1: Actual location + Shift for visibility

                    //FIXME : GetRandCircularVec may be causing recoil to be unnoticeable - each next shot in the burst has a new random circular vector around the target.
                    newTargetLoc += report.GetRandCircularVec();

                    // ----------------------------------- STEP 2: Estimated shot to hit location

                    newTargetLoc = sourceLoc + (newTargetLoc - sourceLoc).normalized * estimatedTargDist;

                    // Lead a moving target
                    if (!isInstant) {

                        newTargetLoc += report.GetRandLeadVec();
                    }
                }

                // ----------------------------------- STEP 3: Recoil, Skewing, Skill checks, Cover calculations

                rotationDegrees = 0f;
                angleRadians = 0f;
                if (numShotsFired < CompHyperburst.HyperburstShotCount) {
                    if (numShotsFired == 0) {
                        GetSwayVec(ref rotationDegrees, ref angleRadians);
                    } else {
                        GetHyperburstSwayVec(ref rotationDegrees, ref angleRadians);
                    }
                    GetHyperburstRecoilVec(ref rotationDegrees, ref angleRadians, CompHyperburst.HyperburstRecoilFactor);
                } else if (numShotsFired == CompHyperburst.HyperburstShotCount) {
                    var enhancedRecoil = (1f - CompHyperburst.HyperburstRecoilFactor) * CompHyperburst.HyperburstShotCount;

                    GetSwayVec(ref rotationDegrees, ref angleRadians);
                    GetHyperburstRecoilVec(ref rotationDegrees, ref angleRadians, enhancedRecoil);
                } else {
                    GetSwayVec(ref rotationDegrees, ref angleRadians);
                    GetRecoilVec(ref rotationDegrees, ref angleRadians);
                }

                // Height difference calculations for ShotAngle

                var targetHeight = GetTargetHeight(report.target, report.cover, report.roofed, v);

                if (!LockRotationAndAngle) {
                    lastShotAngle = ShotAngle(u.WithY(ShotHeight), newTargetLoc.ToVector3().WithY(targetHeight));
                }
                angleRadians += lastShotAngle;
            }

            // ----------------------------------- STEP 4: Mechanical variation

            // Get shotvariation, in angle Vector2 RADIANS.
            Vector2 spreadVec = (projectilePropsCE.isInstant && projectilePropsCE.damageFalloff) ? new Vector2(0, 0) : report.GetRandSpreadVec();
            // ----------------------------------- STEP 5: Finalization

            if (!LockRotationAndAngle) {
                lastShotRotation = ShotRotation(newTargetLoc.ToVector3());
            }
            shotRotation = (lastShotRotation + rotationDegrees + spreadVec.x) % 360;
            shotAngle = angleRadians + spreadVec.y * Mathf.Deg2Rad;
            distance = (newTargetLoc - sourceLoc).magnitude;
            sourceLoc += IncrementBarrelCount();
        }

        /// <summary>
        /// Calculates the amount of recoil at a given point in a burst, up to a maximum
        /// </summary>
        /// <param name="rotation">The ref float to have horizontal recoil in degrees added to.</param>
        /// <param name="angle">The ref float to have vertical recoil in radians added to.</param>
        private void GetHyperburstRecoilVec(ref float rotation, ref float angle, float recoilFactor) {
            var recoil = RecoilAmount * recoilFactor;
            float maxX = recoil * 0.5f;
            float minX = -maxX;
            float maxY = recoil;
            float minY = -recoil / 3;

            float recoilMagnitude = numShotsFired == 0 ? 0 : Mathf.Pow((5 - ShootingAccuracy), (Mathf.Min(10, numShotsFired) / 6.25f));
            float nextRecoilMagnitude = Mathf.Pow((5 - ShootingAccuracy), (Mathf.Min(10, numShotsFired + 1) / 6.25f));

            rotation += recoilMagnitude * Rand.Range(minX, maxX);
            var trd = Rand.Range(minY, maxY);
            angle += recoilMagnitude * Mathf.Deg2Rad * trd;
            lastRecoilDeg += nextRecoilMagnitude * trd;
        }
        /// <summary>
        /// Calculates current weapon sway based on a parametric function with maximum amplitude depending on shootingAccuracy and scaled by weapon's swayFactor using the stazrt of the hyperburst.
        /// </summary>
        /// <param name="rotation">The ref float to have horizontal sway in degrees added to.</param>
        /// <param name="angle">The ref float to have vertical sway in radians added to.</param>
        private void GetHyperburstSwayVec(ref float rotation, ref float angle) {
            float num = hyperburstStartTick + Shooter.thingIDNumber;
            rotation += SwayAmplitude * Mathf.Sin(num * 0.022f);
            angle += 0.004363323f * SwayAmplitude * Mathf.Sin(num * 0.0165f);
        }
    }
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryCastNextBurstShot))]
    internal static class Verb_TryCastNextBurstShot_Hyperburst {

        private static readonly MethodInfo method_GetActualTicksBetweenBurstShots = AccessTools.Method(typeof(Verb_TryCastNextBurstShot_Hyperburst), "GetActualTicksBetweenBurstShots");
        private static readonly FieldInfo field_ticksToNextBurstShot = AccessTools.Field(typeof(Verb), "ticksToNextBurstShot");
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            
            var codes = new List<CodeInstruction>(instructions);
            var target = FindInsertionTarget(codes);
            //codes[target -1] = new CodeInstruction(OpCodes.Nop);
            codes[target] = new CodeInstruction(OpCodes.Nop);
            codes.InsertRange(target, new CodeInstruction[] {
                //new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, method_GetActualTicksBetweenBurstShots),
            });
            return codes.AsEnumerable();
        }
        private static int FindInsertionTarget(List<CodeInstruction> codes) {
            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].StoresField(field_ticksToNextBurstShot)) {
                    return i - 1;
                }
            }
            return -1;
        }
        private static int GetActualTicksBetweenBurstShots(Verb verb) {
            var verb_ShootCEV = verb as Verb_ShootCEV;
            if (verb_ShootCEV == null 
                || verb_ShootCEV.CompHyperburst == null 
                || verb_ShootCEV.CompFireModes == null 
                || (verb_ShootCEV.CompFireModes.CurrentFireMode == FireMode.AutoFire 
                    && !verb_ShootCEV.CompHyperburst.ApplyDuringAuto)
            ) {
                return verb.verbProps.ticksBetweenBurstShots;
            }
            if (verb_ShootCEV.numShotsFired < verb_ShootCEV.CompHyperburst.HyperburstShotCount) {
                return verb_ShootCEV.CompHyperburst.TicksBetweenHyperburstShots;
            }
            return verb.verbProps.ticksBetweenBurstShots;
        }
    }

    [HarmonyPatch]
    internal static class ThingDef_Description_Hyperburst {
        private const string BurstShotFireRate = "BurstShotFireRate";

        private static System.Type? type;
        private static AccessTools.FieldRef<object, VerbProperties>? weaponField;
        private static AccessTools.FieldRef<object, ThingDef>? thisField;
        private static AccessTools.FieldRef<object, StatDrawEntry>? currentField;

        static MethodBase TargetMethod() {
            type = typeof(ThingDef).GetNestedTypes(AccessTools.all).FirstOrDefault(x => x.Name.Contains("<SpecialDisplayStats>"));
            weaponField = AccessTools.FieldRefAccess<VerbProperties>(type, AccessTools.GetFieldNames(type).FirstOrDefault(x => x.Contains("<verb>")));
            thisField = AccessTools.FieldRefAccess<ThingDef>(type, AccessTools.GetFieldNames(type).FirstOrDefault(x => x.Contains("this")));
            currentField = AccessTools.FieldRefAccess<StatDrawEntry>(type, AccessTools.GetFieldNames(type).FirstOrDefault(x => x.Contains("current")));

            return AccessTools.Method(type, "MoveNext");
        }

        public static void Postfix(IEnumerator<StatDrawEntry> __instance, ref bool __result) {
            if (__result) {
                var entry = __instance.Current;
                if (entry.LabelCap.Contains(BurstShotFireRate.Translate().CapitalizeFirst())) {
                    var def = thisField(__instance);
                    var compProps = def.GetCompProperties<CompProperties_Hyperburst>();

                    if (compProps != null) {
                        var ticksBetweenHyperburstShots = compProps.ticksBetweenHyperburstShots;
                        var ticksBetweenBurstShots = weaponField(__instance).ticksBetweenBurstShots;

                        // Include hyperburst
                        if (ticksBetweenHyperburstShots != ticksBetweenBurstShots) {
                            entry.valueStringInt = string.Format("{0} / {1} rpm", 3600 / ticksBetweenHyperburstShots, 3600 / ticksBetweenBurstShots);
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(CompFireSelection), "OptimizeModes")]
    static class CompFireSelection_OptimizeModes_Hyperburst {
        static void Postfix(CompFireModes fireModes, Verb verb, LocalTargetInfo castTarg, LocalTargetInfo destTarg) {
            var shootCEV = verb as Verb_ShootCEV;
            if (shootCEV == null) { return; }
            if (shootCEV.compHyperburst == null) { return; }
            if (fireModes.CurrentFireMode == FireMode.SingleFire) {
                fireModes.TrySetFireMode(FireMode.BurstFire);
            }
            if (fireModes.CurrentFireMode == FireMode.AutoFire && shootCEV.compHyperburst.ApplyDuringAuto == false) {
                fireModes.TrySetFireMode(FireMode.BurstFire);
            }
        }
    }
}
