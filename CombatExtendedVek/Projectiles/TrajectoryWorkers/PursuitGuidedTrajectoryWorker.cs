using CombatExtended;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace CombatExtendedVek.Projectiles.TrajectoryWorkers {
    public class PursuitGuidedTrajectoryWorker : BallisticsTrajectoryWorker {
        public override bool GuidedProjectile => true;
        //TODO: Patch Shift Target to not apply lead when using this worker
        public override void ReactiveAcceleration(ProjectileCE projectile) {
            LocalTargetInfo currentTarget = projectile.intendedTarget;
            if (currentTarget.ThingDestroyed) {
                base.ReactiveAcceleration(projectile);
                return;
            }
            
            var targetPos = currentTarget.Thing?.DrawPos ?? currentTarget.Cell.ToVector3Shifted();
            //Swap to pure pursit right before impact to make juking more difficult
            if (TimeToTarget(projectile) * GenTicks.TicksPerRealSecond >= 10) { 
                targetPos = TargetLeadLocation(projectile);
            }
            targetPos.y = TargetHeight(projectile);
            /*
             * Take perfect vector and current vector. 
             * Scale perfect vector to the current magnitude.
             * Subtract perfect vector from current vector.
             * Modify current vector as result vector by the budgeted amount or until it is 0.
             */
            //targetPos.y = TargetHeight(projectile);
            //var perfectPursuitVector = targetPos - projectile.ExactPosition;
            var startingMagnitude = projectile.velocity.magnitude;
            //var deltaPursuit = perfectPursuitVector - (projectile.velocity.normalized * perfectPursuitVector.magnitude);
            var forwardSpeed = SpeedGain(projectile);
            var directionalAdjustment = (SpeedGain(projectile) * 0.1f) + (startingMagnitude * 0.6f * GenTicks.TicksPerRealSecond);

            UpdateProjectileheading(projectile, directionalAdjustment);

            //projectile.velocity += deltaPursuit.normalized * directionalAdjustment / GenTicks.TicksPerRealSecond / GenTicks.TicksPerRealSecond;
            //projectile.velocity = projectile.velocity.normalized * startingMagnitude;
            var forwardVector = projectile.velocity.normalized * forwardSpeed;
            forwardVector.y = 0;
            if (projectile.fuelTicks >= 1) {
                projectile.velocity += forwardVector / GenTicks.TicksPerRealSecond / GenTicks.TicksPerRealSecond;
            }
            //Log.Message(String.Format("Velocity: {0:0.0}", projectile.velocity.magnitude * GenTicks.TicksPerRealSecond));
            projectile.fuelTicks--;
        }
        public void UpdateProjectileheading(ProjectileCE projectile, float adjustment) {
            LocalTargetInfo currentTarget = projectile.intendedTarget;
            var targetPosition = currentTarget.Thing?.DrawPos ?? currentTarget.Cell.ToVector3Shifted();
            //Swap to pure pursit right before impact to make juking more difficult
            var ticksToTarget = TimeToTarget(projectile) * GenTicks.TicksPerRealSecond;
            if (ticksToTarget >= 1) {
                targetPosition = TargetLeadLocation(projectile);
            }
            targetPosition.y = TargetHeight(projectile);

            var adjustmentBudget = adjustment / GenTicks.TicksPerRealSecond / GenTicks.TicksPerRealSecond;
            var remainingAdjustment = adjustmentBudget;

            var perfectPursuitVector = targetPosition - projectile.ExactPosition;
            perfectPursuitVector.Normalize();
            //Scale perfect pursuit to the magnitude of the current velocity
            perfectPursuitVector *= projectile.velocity.magnitude;
            var deltaPursuit = perfectPursuitVector - projectile.velocity;
            //Correct Height (Y)
            if (ticksToTarget >= 30) {
                if (MathF.Abs(deltaPursuit.y) <= remainingAdjustment) {
                    remainingAdjustment -= MathF.Abs(deltaPursuit.y);
                    projectile.velocity.y += deltaPursuit.y;
                    //Log.Message(string.Format("1 - Remaining Value: {0,8:F5}", remainingAdjustment));
                } else {
                    projectile.velocity.y += MathF.CopySign(remainingAdjustment, deltaPursuit.y);
                    remainingAdjustment -= remainingAdjustment;
                    //Log.Message(string.Format("2 - Remaining Value: {0,8:F5}", remainingAdjustment));
                }
            }
            //Correct Horizontal
            var xRatio = MathF.Abs(deltaPursuit.x) / (MathF.Abs(deltaPursuit.x) + MathF.Abs(deltaPursuit.z));
            var zRatio = MathF.Abs(deltaPursuit.z) / (MathF.Abs(deltaPursuit.x) + MathF.Abs(deltaPursuit.z));
            xRatio = float.IsNaN(xRatio) ? 0 : xRatio;
            zRatio = float.IsNaN(zRatio) ? 0 : zRatio;
            xRatio = float.IsInfinity(xRatio) ? 0 : xRatio;
            zRatio = float.IsInfinity(zRatio) ? 0 : zRatio;

            //Log.Message(string.Format("xRatio: {0,8:F5} | zRatio: {1,8:F5} | Sum: {2,8:F5}", xRatio, zRatio, xRatio + zRatio));

            if (xRatio >= zRatio) {
                if (MathF.Abs(deltaPursuit.z) <= remainingAdjustment * zRatio) {
                    remainingAdjustment -= MathF.Abs(deltaPursuit.z);
                    projectile.velocity.z += deltaPursuit.z;
                    //Log.Message(string.Format("3 - Remaining Value: {0,8:F5}", remainingAdjustment));
                } else {
                    var adjustmentAmount = remainingAdjustment * zRatio;
                    remainingAdjustment -= adjustmentAmount;
                    projectile.velocity.z += MathF.CopySign(adjustmentAmount, deltaPursuit.z);
                    //Log.Message(string.Format("4 - Remaining Value: {0,8:F5}", remainingAdjustment));
                }
                if (MathF.Abs(deltaPursuit.x) <= remainingAdjustment) {
                    remainingAdjustment -= MathF.Abs(deltaPursuit.x);
                    projectile.velocity.x += deltaPursuit.x;
                    //Log.Message(string.Format("5 - Remaining Value: {0,8:F5}", remainingAdjustment));
                } else {
                    projectile.velocity.x += MathF.CopySign(remainingAdjustment, deltaPursuit.x);
                    remainingAdjustment -= remainingAdjustment;
                    //Log.Message(string.Format("6 - Remaining Value: {0,8:F5}", remainingAdjustment));
                }
            } else {
                if (MathF.Abs(deltaPursuit.x) <= remainingAdjustment * xRatio) {
                    remainingAdjustment -= MathF.Abs(deltaPursuit.x);
                    projectile.velocity.x += deltaPursuit.x;
                    //Log.Message(string.Format("7 - Remaining Value: {0,8:F5}", remainingAdjustment));
                } else {
                    var adjustmentAmount = remainingAdjustment * xRatio;
                    remainingAdjustment -= adjustmentAmount;
                    projectile.velocity.x += MathF.CopySign(adjustmentAmount, deltaPursuit.x);
                    //Log.Message(string.Format("8 - Remaining Value: {0,8:F5}", remainingAdjustment));
                }
                if (MathF.Abs(deltaPursuit.z) <= remainingAdjustment) {
                    remainingAdjustment -= MathF.Abs(deltaPursuit.z);
                    projectile.velocity.z += deltaPursuit.z;
                    //Log.Message(string.Format("9 - Remaining Value: {0,8:F5}", remainingAdjustment));
                } else {
                    projectile.velocity.z += MathF.CopySign(remainingAdjustment, deltaPursuit.z);
                    remainingAdjustment -= remainingAdjustment;
                    //Log.Message(string.Format("10- Remaining Value: {0,8:F5}", remainingAdjustment));
                }
            }
            var finalDeltaPursuit = perfectPursuitVector - projectile.velocity;
            /*
            Log.Message(String.Format("Delta Vector: {0,8:F5} {1,8:F5} {2,8:F5} | Adjustment Value: {3,8:F5} || Final Delta Vector: {4,8:F5} {5,8:F5} {6,8:F5} | Remaining Value: {7,8:F5}",
                deltaPursuit.x,
                deltaPursuit.y,
                deltaPursuit.z,
                adjustmentBudget,
                finalDeltaPursuit.x,
                finalDeltaPursuit.y,
                finalDeltaPursuit.z,
                remainingAdjustment));
            */
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SpeedGain(ProjectileCE projectile) {
            return projectile.fuelTicks >= 1 ? projectile.Props.speedGain : 0f;
        }

        private Vector3 TargetPosition(ProjectileCE projectile) {
            var targetPos = projectile.intendedTarget.Thing?.DrawPos ?? projectile.intendedTarget.Cell.ToVector3Shifted();
            targetPos.y = TargetHeight(projectile);
            return targetPos;
        }

        private float TargetHeight(ProjectileCE projectile) {
            //Calculate target Height
            var victimVert = new CollisionVertical(projectile.intendedTarget.Thing);
            var targetRange = victimVert.HeightRange;
            targetRange.min = victimVert.BottomHeight;
            targetRange.max = victimVert.MiddleHeight; 
            // Target Upper Head area
            targetRange.min = targetRange.Average;
            targetRange.max = victimVert.Max;
            var targetHeight = targetRange.Average;
            if (targetHeight > CollisionVertical.WallCollisionHeight) {
                targetHeight = CollisionVertical.WallCollisionHeight;
            }
            return targetHeight;
        }

        private float TimeToTarget(ProjectileCE projectile) {
            var distance = Vector3.Distance(TargetPosition(projectile), projectile.ExactPosition);
            var initialVeloicity = projectile.velocity.magnitude * GenTicks.TicksPerRealSecond;
            var time = distance / initialVeloicity;
            //Log.Message(String.Format("Time To Target: {0:0.0}", time));
            return time;
        }

        private Vector3 TargetLeadLocation(ProjectileCE projectile) {
            var maxLeadTime = 0.5f;
            var target = projectile.intendedTarget;
            var targetPosition = projectile.intendedTarget.Thing?.DrawPos ?? projectile.intendedTarget.Cell.ToVector3Shifted();
            var targetPawn = target.Pawn;
            if (TargetIsMoving(targetPawn)) {
                var leadDistance = CE_Utility.GetMoveSpeed(targetPawn) * MathF.Min(maxLeadTime, TimeToTarget(projectile));
                var leadOffset = (targetPawn.pather.nextCell - targetPawn.Position).ToVector3() * leadDistance;
                return targetPosition + leadOffset;
            }
            return targetPosition;
        }

        private bool TargetIsMoving(Pawn target) {
            return target != null && target.pather != null && target.pather.Moving && (target.stances.stunner == null || !target.stances.stunner.Stunned);
        }

        [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.ShiftTarget), new Type[] { typeof(ShiftVecReport), typeof(Vector3), typeof(bool), typeof(bool) })]
        private static class Verb_LaunchProjectileCE_ShiftTarget_PursuitGuidedTrajectoryWorker_Patch {
            static bool Prefix(Verb_LaunchProjectileCE __instance, ref bool isInstant) {
                if (__instance.projectilePropsCE.trajectoryWorker == typeof(PursuitGuidedTrajectoryWorker)) {
                    isInstant = true;
                }
                return true;
            }
        }
    }
}
