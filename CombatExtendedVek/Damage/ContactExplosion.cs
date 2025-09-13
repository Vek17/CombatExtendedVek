using CombatExtended;
using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using System.Text;
using Verse;

namespace CombatExtendedVek.Damage {
    public class ContactExplosion : SecondaryDamage {
        public float penetration = 0f;

        public new DamageInfo GetDinfo(DamageInfo primaryDinfo) {
            var dinfo = new DamageInfo(def,
                                       amount,
                                       penetration,
                                       primaryDinfo.Angle,
                                       primaryDinfo.Instigator,
                                       null, //primaryDinfo.HitPart,
                                       primaryDinfo.Weapon,
                                       instigatorGuilty: primaryDinfo.InstigatorGuilty);
            dinfo.SetBodyRegion(primaryDinfo.Height, primaryDinfo.Depth);
            return dinfo;
        }

        [HarmonyPatch(typeof(SecondaryDamage), nameof(SecondaryDamage.GetDinfo), new Type[] { typeof(DamageInfo) })]
        private static class SecondaryDamage_GetDinfo_ContactExplosion {
            static bool Prefix(SecondaryDamage __instance, DamageInfo primaryDinfo, ref DamageInfo __result) {
                var detailed = __instance as ContactExplosion;
                if (detailed == null) { return true; }
                __result = detailed.GetDinfo(primaryDinfo);
                return false;
            }
        }
        [HarmonyPatch(typeof(SecondaryDamage), nameof(SecondaryDamage.GetDinfo), new Type[] {})]
        private static class SecondaryDamage_GetDinfo2_ContactExplosion {
            static bool Prefix(SecondaryDamage __instance, ref DamageInfo __result) {
                var detailed = __instance as ContactExplosion;
                if (detailed == null) { return true; }
                __result = new DamageInfo(detailed.def, detailed.amount, armorPenetration: detailed.penetration);
                return false;
            }
        }
        [HarmonyPatch(typeof(AmmoUtility), nameof(AmmoUtility.GetProjectileReadout))]
        private static class AmmoUtility_AmmoUtility_ContactExplosion {
            static bool Prefix(ThingDef projectileDef, Thing weapon, ref string __result) {
                // Append ammo stats
                var props = projectileDef?.projectile as ProjectilePropertiesCE;
                if (props == null || projectileDef == null) { return true; }
                if (!props.secondaryDamage.ContainsAny(damage => damage is ContactExplosion)) {
                    return true;
                }

                var multiplier = weapon?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;
                var stringBuilder = new StringBuilder();
                // Damage type/amount
                var dmgList = "   " + "CE_DescDamage".Translate() + ": ";
                if (props.secondaryDamage.Where(damage => damage is not ContactExplosion).Any()) {
                    stringBuilder.AppendLine(dmgList);
                    stringBuilder.AppendLine("   " + GenText.ToStringByStyle(props.GetDamageAmount(weapon), ToStringStyle.Integer) + " (" + props.damageDef.LabelCap + ")");
                    foreach (var sec in props.secondaryDamage) {
                        var secondaryChance = sec.chance >= 1.0f ? "" : $"({GenText.ToStringByStyle(sec.chance, ToStringStyle.PercentZero)} {"CE_Chance".Translate()})";
                        stringBuilder.AppendLine($"   {GenText.ToStringByStyle(sec.amount, ToStringStyle.Integer)} ({sec.def.LabelCap}) {secondaryChance}");
                    }
                } else {
                    stringBuilder.AppendLine(dmgList + GenText.ToStringByStyle(props.GetDamageAmount(weapon), ToStringStyle.Integer) + " (" + props.damageDef.LabelCap + ")");
                }
                // Explosion radius
                if (props.explosionRadius > 0) {
                    stringBuilder.AppendLine("   " + "CE_DescExplosionRadius".Translate() + ": " + props.explosionRadius.ToStringByStyle(ToStringStyle.FloatOne));
                }
                // Thermal/Electric Penetration
                if ((props.damageDef.armorCategory == CE_DamageArmorCategoryDefOf.Heat
                            || props.damageDef.armorCategory == CE_DamageArmorCategoryDefOf.Electric) && props.damageDef.defaultArmorPenetration > 0f) {
                    stringBuilder.AppendLine("   " + "CE_DescAmbientPenetration".Translate() + ": " + (props.damageDef.defaultArmorPenetration).ToStringByStyle(ToStringStyle.PercentZero));
                }
                // Sharp / blunt AP
                if (props.damageDef.armorCategory != CE_DamageArmorCategoryDefOf.Heat
                        && props.damageDef.armorCategory != CE_DamageArmorCategoryDefOf.Electric
                        && props.damageDef != DamageDefOf.Stun
                        && props.damageDef != DamageDefOf.Extinguish
                        && props.damageDef != DamageDefOf.Smoke
                        && props.GetDamageAmount(weapon) != 0) {
                    if (props.explosionRadius > 0) {
                        stringBuilder.AppendLine("   " + "CE_DescBluntPenetration".Translate() + ": " + props.GetExplosionArmorPenetration() + " " + "CE_MPa".Translate());
                    } else {
                        stringBuilder.AppendLine("   " + "CE_DescSharpPenetration".Translate() + ": " + (props.armorPenetrationSharp * multiplier).ToStringByStyle(ToStringStyle.FloatTwo) + " " + "CE_mmRHA".Translate());
                        stringBuilder.AppendLine("   " + "CE_DescBluntPenetration".Translate() + ": " + (props.armorPenetrationBlunt * multiplier).ToStringByStyle(ToStringStyle.FloatTwo) + " " + "CE_MPa".Translate());
                    }
                }
                // Contact explosion
                foreach (var contactExplosion in props.secondaryDamage.OfType<ContactExplosion>()) {
                    stringBuilder.AppendLine("   " + "CEV_DescContactExplosion".Translate() + ":");
                    stringBuilder.AppendLine("   " + "   " + "CE_DescDamage".Translate() + ": " + contactExplosion.amount + " (" + contactExplosion.def.LabelCap + ")");
                    stringBuilder.AppendLine("   " + "   " + "CE_DescBluntPenetration".Translate() + ": " + contactExplosion.penetration.ToStringByStyle(ToStringStyle.FloatTwo) + " " + "CE_MPa".Translate());
                }
                // Secondary explosion
                var secExpProps = projectileDef.GetCompProperties<CompProperties_ExplosiveCE>();
                if (secExpProps != null) {
                    if (secExpProps.explosiveRadius > 0) {
                        stringBuilder.AppendLine("   " + "CE_DescSecondaryExplosion".Translate() + ":");
                        stringBuilder.AppendLine("   " + "   " + "CE_DescDamage".Translate() + ": " + secExpProps.damageAmountBase.ToStringByStyle(ToStringStyle.Integer) + " (" + secExpProps.explosiveDamageType.LabelCap + ")");
                        stringBuilder.AppendLine("   " + "   " + "CE_DescExplosionRadius".Translate() + ": " + secExpProps.explosiveRadius.ToStringByStyle(ToStringStyle.FloatOne));
                        stringBuilder.AppendLine("   " + "   " + "CE_DescBluntPenetration".Translate() + ": " + secExpProps.GetExplosionArmorPenetration().ToStringByStyle(ToStringStyle.FloatTwo) + " " + "CE_MPa".Translate());
                    }
                }

                // Pellets
                if (props.pelletCount > 1) {
                    stringBuilder.AppendLine("   " + "CE_DescPelletCount".Translate() + ": " + GenText.ToStringByStyle(props.pelletCount, ToStringStyle.Integer));
                }
                if (props.spreadMult != 1) {
                    stringBuilder.AppendLine("   " + "CE_DescSpreadMult".Translate() + ": " + props.spreadMult.ToStringByStyle(ToStringStyle.PercentZero));
                }
                if (props.recoilMultiplier != 1) {
                    stringBuilder.AppendLine("   " + "CE_DescRecoilMult".Translate() + ": " + props.recoilMultiplier.ToStringByStyle(ToStringStyle.PercentZero));
                }
                if (props.recoilOffset != 0) {
                    stringBuilder.AppendLine("   " + "CE_DescRecoilOffset".Translate() + ": " + props.recoilOffset.ToStringByStyle(ToStringStyle.FloatMaxOne));
                }
                if (props.effectiveRangeMultiplier != 1) {
                    stringBuilder.AppendLine("   " + "CE_DescEffectiveRangeMult".Translate() + ": " + props.effectiveRangeMultiplier.ToStringByStyle(ToStringStyle.PercentZero));
                }
                if (props.effectiveRangeOffset != 0) {
                    stringBuilder.AppendLine("   " + "CE_DescEffectiveRangeOffset".Translate() + ": " + props.effectiveRangeOffset.ToStringByStyle(ToStringStyle.FloatMaxOne));
                }
                if (props.warmupMultiplier != 1) {
                    stringBuilder.AppendLine("   " + "CE_DescWarmupMult".Translate() + ": " + props.warmupMultiplier.ToStringByStyle(ToStringStyle.PercentZero));
                }
                if (props.warmupOffset != 0) {
                    stringBuilder.AppendLine("   " + "CE_DescWarmupOffset".Translate() + ": " + props.warmupOffset.ToStringByStyle(ToStringStyle.FloatMaxOne));
                }
                if (props.muzzleFlashMultiplier != 1) {
                    stringBuilder.AppendLine("   " + "CE_DescMuzzleFlashMult".Translate() + ": " + props.muzzleFlashMultiplier.ToStringByStyle(ToStringStyle.PercentZero));
                }
                if (props.muzzleFlashOffset != 0) {
                    stringBuilder.AppendLine("   " + "CE_DescMuzzleFlashOffset".Translate() + ": " + props.muzzleFlashOffset.ToStringByStyle(ToStringStyle.FloatMaxOne));
                }

                // Fragments
                var fragmentComp = projectileDef.GetCompProperties<CompProperties_Fragments>();
                if (fragmentComp != null) {
                    stringBuilder.AppendLine("   " + "CE_DescFragments".Translate() + ":");
                    foreach (var fragmentDef in fragmentComp.fragments) {
                        var fragmentProps = fragmentDef?.thingDef?.projectile as ProjectilePropertiesCE;
                        stringBuilder.AppendLine("   " + "   " + fragmentDef?.LabelCap);
                        stringBuilder.AppendLine("   " + "   " + "   " + "CE_DescDamage".Translate() + ": " + fragmentProps?.damageAmountBase.ToString() + " (" + fragmentProps?.damageDef.LabelCap.ToString() + ")");
                        stringBuilder.AppendLine("   " + "   " + "   " + "CE_DescSharpPenetration".Translate() + ": " + fragmentProps?.armorPenetrationSharp.ToStringByStyle(ToStringStyle.FloatTwo) + " " + "CE_mmRHA".Translate());
                        stringBuilder.AppendLine("   " + "   " + "   " + "CE_DescBluntPenetration".Translate() + ": " + fragmentProps?.armorPenetrationBlunt.ToStringByStyle(ToStringStyle.FloatTwo) + " " + "CE_MPa".Translate());
                    }
                }

                __result = stringBuilder.ToString();
                return false;
            }
        }

        
    }
}
