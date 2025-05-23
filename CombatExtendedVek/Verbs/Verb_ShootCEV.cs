using CombatExtended;
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

        public virtual CompHyperburst? CompHyperburst {
            get {
                if (CompHyperburst == null && EquipmentSource != null) {
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
            if (CompFireModes == null || CompFireModes.CurrentFireMode == FireMode.SingleFire) {
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

                // ----------------------------------- STEP 3: Recoil, Skewing, Skill checks, Cover calculations

                rotationDegrees = 0f;
                angleRadians = 0f;
                if (numShotsFired < CompHyperburst.HyperburstShotCount - 1) {
                    if (numShotsFired == 0) {
                        GetSwayVec(ref rotationDegrees, ref angleRadians);
                    }
                    GetHyperburstRecoilVec(ref rotationDegrees, ref angleRadians);
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
        }

        /// <summary>
        /// Calculates the amount of recoil at a given point in a burst, up to a maximum
        /// </summary>
        /// <param name="rotation">The ref float to have horizontal recoil in degrees added to.</param>
        /// <param name="angle">The ref float to have vertical recoil in radians added to.</param>
        private void GetHyperburstRecoilVec(ref float rotation, ref float angle) {
            var recoil = RecoilAmount * 0.1f;
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
    }
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryCastNextBurstShot))]
    public static class Hyperburst_VerbPatch {

        private static readonly MethodInfo method_GetActualTicksBetweenBurstShots = AccessTools.Method(typeof(Hyperburst_VerbPatch), "GetActualTicksBetweenBurstShots");
        private static readonly FieldInfo field_ticksToNextBurstShot = AccessTools.Field(typeof(Verb), "ticksToNextBurstShot");
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            
            var codes = new List<CodeInstruction>(instructions);
            var target = FindInsertionTarget(codes);
            codes[target] = new CodeInstruction(OpCodes.Nop);
            codes.InsertRange(target, new CodeInstruction[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, method_GetActualTicksBetweenBurstShots),
            });
            return codes.AsEnumerable();
        }
        private static int FindInsertionTarget(List<CodeInstruction> codes) {
            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode == OpCodes.Ldfld && codes[i].LoadsField(field_ticksToNextBurstShot)) {
                    return i - 1;
                }
            }
            return -1;
        }
        private static int GetActualTicksBetweenBurstShots(Verb verb) {
            var verb_ShootCEV = verb as Verb_ShootCEV;
            if (verb_ShootCEV == null || verb_ShootCEV.CompHyperburst == null) {
                return verb.verbProps.ticksBetweenBurstShots;
            }
            if (verb_ShootCEV.numShotsFired < verb_ShootCEV.CompHyperburst.HyperburstShotCount - 1) {
                return verb_ShootCEV.CompHyperburst.TicksBetweenHyperburstShots;
            }
            return verb.verbProps.ticksBetweenBurstShots;
        }
    }
}
