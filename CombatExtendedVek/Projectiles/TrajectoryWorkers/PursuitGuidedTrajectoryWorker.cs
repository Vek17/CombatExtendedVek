using CombatExtended;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static System.Diagnostics.ConfigurationManagerInternalFactory;
using static UnityEngine.GraphicsBuffer;

namespace CombatExtendedVek.Projectiles.TrajectoryWorkers {
    public class PursuitGuidedTrajectoryWorker : BallisticsTrajectoryWorker {
        public override void ReactiveAcceleration(ProjectileCE projectile) {
            LocalTargetInfo currentTarget = projectile.intendedTarget;
            if (currentTarget.ThingDestroyed) {
                base.ReactiveAcceleration(projectile);
                return;
            }
            
            var targetPos = currentTarget.Thing?.DrawPos ?? currentTarget.Cell.ToVector3Shifted();
            //Swap to pure pursit right before impact to make juking more difficult
            if (TimeToTarget(projectile) * GenTicks.TicksPerRealSecond >= 5) { 
                targetPos = TargetLeadLocation(projectile);
            }

            //
            targetPos.y = TargetHeight(projectile);
            var perfectPursuitVector = targetPos - projectile.ExactPosition;
            var startingMagnitude = projectile.velocity.magnitude;
            var deltaPursuit = perfectPursuitVector - (projectile.velocity.normalized * perfectPursuitVector.magnitude);
            var forwardSpeed = SpeedGain(projectile);
            var directionalAdjustment = (SpeedGain(projectile) * 0.1f) + (startingMagnitude * 0.5f * GenTicks.TicksPerRealSecond);
            projectile.velocity += deltaPursuit.normalized * directionalAdjustment / GenTicks.TicksPerRealSecond / GenTicks.TicksPerRealSecond;
            projectile.velocity = projectile.velocity.normalized * startingMagnitude;
            if (projectile.fuelTicks >= 1) {
                projectile.velocity += projectile.velocity.normalized * forwardSpeed / GenTicks.TicksPerRealSecond / GenTicks.TicksPerRealSecond;
            }
            projectile.fuelTicks--;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SpeedGain(ProjectileCE projectile) {
            return projectile.fuelTicks >= 1 ? projectile.Props.speedGain : 0f;
        }
        public override bool GuidedProjectile => true;

        private Vector3 TargetPosition(ProjectileCE projectile) {
            var targetPos = projectile.intendedTarget.Thing?.DrawPos ?? projectile.intendedTarget.Cell.ToVector3Shifted();
            targetPos.y = TargetHeight(projectile);
            return targetPos;
        }

        private float TimeToTarget(ProjectileCE projectile) {
            var distance = Vector3.Distance(TargetPosition(projectile), projectile.ExactPosition);
            var initialVeloicity = projectile.velocity.magnitude * GenTicks.TicksPerRealSecond;
            var time = distance / initialVeloicity;
            //Log.Message(String.Format("Time To Target: {0:0.0}", time));
            return time;
        }

        private float TargetHeight(ProjectileCE projectile) {
            //Calculate target Height
            var victimVert = new CollisionVertical(projectile.intendedTarget.Thing);
            var targetRange = victimVert.HeightRange;
            targetRange.min = victimVert.MiddleHeight;
            targetRange.max = victimVert.BottomHeight;
            // Target Upper Head area
            targetRange.min = targetRange.RandomInRange;
            targetRange.max = victimVert.Max;
            var targetHeight = targetRange.Average;
            if (targetHeight > CollisionVertical.WallCollisionHeight) {
                targetHeight = CollisionVertical.WallCollisionHeight;
            }
            return targetHeight;
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
    }
}
