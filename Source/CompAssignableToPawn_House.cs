using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Homestead
{
    public class CompAssignableToPawn_House : CompAssignableToPawn
    {
        private bool expandingFamily;

        public CompHouseMarker Marker => parent.GetComp<CompHouseMarker>();

        public override IEnumerable<Pawn> AssigningCandidates
        {
            get
            {
                if (!parent.Spawned)
                {
                    return Enumerable.Empty<Pawn>();
                }
                return parent.Map.mapPawns.FreeColonists.OrderByDescending(AssignedAnything);
            }
        }

        public override bool AssignedAnything(Pawn pawn)
        {
            return HouseManager.HouseOf(pawn) != null;
        }

        public override void TryAssignPawn(Pawn pawn)
        {
            UnassignFromAllHouses(pawn);
            base.TryAssignPawn(pawn);
            Marker?.Manager?.SetDirty();

            if (expandingFamily)
            {
                return;
            }
            expandingFamily = true;
            try
            {
                foreach (Pawn member in FamilyOf(pawn))
                {
                    if (!assignedPawns.Contains(member) && assignedPawns.Count < Props.maxAssignedPawnsCount)
                    {
                        TryAssignPawn(member);
                    }
                }
            }
            finally
            {
                expandingFamily = false;
            }
        }

        public override void TryUnassignPawn(Pawn pawn, bool sort = true, bool uninstall = false)
        {
            base.TryUnassignPawn(pawn, sort, uninstall);
            Marker?.Manager?.SetDirty();
        }

        private static void UnassignFromAllHouses(Pawn pawn)
        {
            foreach (Map map in Find.Maps)
            {
                HouseManager manager = map.GetComponent<HouseManager>();
                if (manager == null)
                {
                    continue;
                }
                foreach (CompHouseMarker marker in manager.Markers)
                {
                    if (marker.IsMember(pawn))
                    {
                        marker.Assignable.TryUnassignPawn(pawn);
                    }
                }
            }
        }

        // Partners, own minor children, and partners' minor children.
        private static IEnumerable<Pawn> FamilyOf(Pawn pawn)
        {
            var family = new HashSet<Pawn>();
            foreach (DirectPawnRelation rel in pawn.relations.DirectRelations)
            {
                if (rel.def == PawnRelationDefOf.Spouse
                    || rel.def == PawnRelationDefOf.Fiance
                    || rel.def == PawnRelationDefOf.Lover)
                {
                    Pawn partner = rel.otherPawn;
                    if (IsColonyMember(partner))
                    {
                        family.Add(partner);
                        foreach (Pawn stepChild in MinorChildrenOf(partner))
                        {
                            family.Add(stepChild);
                        }
                    }
                }
            }
            foreach (Pawn child in MinorChildrenOf(pawn))
            {
                family.Add(child);
            }
            family.Remove(pawn);
            return family;
        }

        private static IEnumerable<Pawn> MinorChildrenOf(Pawn pawn)
        {
            return pawn.relations.Children.Where(c =>
                IsColonyMember(c) && c.ageTracker.AgeBiologicalYears < 18);
        }

        private static bool IsColonyMember(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.IsFreeColonist;
        }

        protected override string GetAssignmentGizmoDesc()
        {
            return "Homestead_GizmoAssignDesc".Translate();
        }
    }
}
