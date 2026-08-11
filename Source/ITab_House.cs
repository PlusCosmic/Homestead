using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Homestead
{
    public class ITab_House : ITab
    {
        private const float Pad = 12f;

        public ITab_House()
        {
            size = new Vector2(440f, 400f);
            labelKey = "Homestead_TabHouse";
        }

        private CompHouseMarker Comp => SelThing?.TryGetComp<CompHouseMarker>();

        public override bool IsVisible => Comp != null;

        protected override void FillTab()
        {
            CompHouseMarker comp = Comp;
            if (comp == null)
            {
                return;
            }
            var outer = new Rect(0f, 0f, size.x, size.y).ContractedBy(Pad);
            var listing = new Listing_Standard();
            listing.Begin(outer);

            Text.Font = GameFont.Medium;
            listing.Label(comp.HouseName);
            Text.Font = GameFont.Small;
            if (listing.ButtonText("Homestead_TabRename".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RenameHouse(comp));
            }
            listing.GapLine();

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("Homestead_TabOwnersHeader".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            var owners = comp.Owners;
            if (owners.Count == 0)
            {
                listing.Label("Homestead_InspectNoOwners".Translate());
            }
            else
            {
                foreach (Pawn owner in owners)
                {
                    listing.Label("  " + owner.LabelShortCap + ", " + owner.ageTracker.AgeBiologicalYears);
                }
            }
            listing.GapLine();

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("Homestead_TabStatsHeader".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (!comp.IsFunctional)
            {
                listing.Label("Homestead_InspectOutdoors".Translate());
            }
            else
            {
                listing.Label("Homestead_TabInteriorCells".Translate(comp.RoomCount, comp.InteriorCellCount));
                listing.Label("Homestead_TabYardCells".Translate(comp.yardCells.Count));
                listing.Label("Homestead_InspectImpressiveness".Translate(
                    comp.AvgImpressiveness.ToString("F0"), comp.ImpressivenessStageLabel));
                listing.Label("Homestead_InspectCleanliness".Translate(comp.AvgCleanliness.ToString("F1")));

                float mood = CurrentHouseMood(comp);
                listing.Label("Homestead_TabMoodContribution".Translate(mood.ToStringWithSign("F0")));
            }

            listing.End();
        }

        // Approximate combined mood the house currently gives its owners:
        // the impressiveness stage plus any filth debuff.
        private static float CurrentHouseMood(CompHouseMarker comp)
        {
            float total = 0f;
            ThoughtDef imp = HomesteadDefOf.Homestead_HouseImpressiveness;
            int stage = comp.ImpressivenessStageIndex;
            if (stage >= 0 && stage < imp.stages.Count && imp.stages[stage] != null)
            {
                total += imp.stages[stage].baseMoodEffect;
            }
            ThoughtDef filth = HomesteadDefOf.Homestead_FilthyHouse;
            float clean = comp.AvgCleanliness;
            if (clean < ThoughtWorker_FilthyHouse.SevereThreshold)
            {
                total += filth.stages[1].baseMoodEffect;
            }
            else if (clean < ThoughtWorker_FilthyHouse.Threshold)
            {
                total += filth.stages[0].baseMoodEffect;
            }
            return total;
        }
    }
}
