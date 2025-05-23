using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace CombatExtendedVek.Comps {
    public class CompHyperburst : ThingComp {
        public CompProperties_Hyperburst Props => (CompProperties_Hyperburst)this.props;

        public bool ApplyDuringAuto => Props.applyDuringAuto;
        public int TicksBetweenHyperburstShots => Props.ticksBetweenHyperburstShots;
        public int HyperburstShotCount => Props.hyperburstShotCount;
        public float HyperburstRecoilFactor => Props.hyperburstRecoilFactor;
    }

    public class CompProperties_Hyperburst : CompProperties {
        public bool applyDuringAuto = false;
        public int ticksBetweenHyperburstShots = 2;
        public int hyperburstShotCount = 2;
        public float hyperburstRecoilFactor = 0.1f;

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