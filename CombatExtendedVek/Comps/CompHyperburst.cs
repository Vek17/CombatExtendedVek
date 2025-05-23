using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace CombatExtendedVek.Comps {
    public class CompHyperburst : ThingComp{
        public CompProperties_Hyperburst Props => (CompProperties_Hyperburst)this.props;

        public bool Apply => Props.applyDuringAuto;
        public int TicksBetweenHyperburstShots => Props.ticksBetweenHyperburstShots;
        public int HyperburstShotCount => Props.hyperburstShotCount;
    }

    public class CompProperties_Hyperburst : CompProperties {
        public bool applyDuringAuto;
        public int ticksBetweenHyperburstShots;
        public int hyperburstShotCount;

        /// <summary>
        /// These constructors aren't strictly required if the compClass is set in the XML.
        /// </summary>
        public CompProperties_Hyperburst() {
            this.compClass = typeof(CompHyperburst);
        }

        public CompProperties_Hyperburst(Type compClass) : base(compClass) {
            this.compClass = compClass;
        }
    }
}