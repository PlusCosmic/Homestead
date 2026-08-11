using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Homestead
{
    public class Designator_HouseYard : Designator
    {
        private readonly CompHouseMarker comp;
        private readonly bool remove;

        public Designator_HouseYard(CompHouseMarker comp, bool remove)
        {
            this.comp = comp;
            this.remove = remove;
            defaultLabel = (remove ? "Homestead_GizmoRemoveYard" : "Homestead_GizmoAddYard").Translate();
            defaultDesc = (remove ? "Homestead_GizmoRemoveYardDesc" : "Homestead_GizmoAddYardDesc").Translate();
            icon = ContentFinder<Texture2D>.Get(remove ? "Homestead/UI/YardRemove" : "Homestead/UI/YardAdd");
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneAdd;
            useMouseIcon = true;
        }

        public override DrawStyleCategoryDef DrawStyleCategory =>
            DefDatabase<DrawStyleCategoryDef>.GetNamedSilentFail(remove ? "RemoveZones" : "Zones");

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!comp.parent.Spawned || comp.parent.Map != Map)
            {
                return false;
            }
            if (!loc.InBounds(Map) || loc.Fogged(Map))
            {
                return false;
            }
            if (remove)
            {
                return comp.yardCells.Contains(loc);
            }
            if (comp.yardCells.Contains(loc))
            {
                return false;
            }
            if (comp.yardCells.Count >= HomesteadSettings.MaxYardCells)
            {
                return false;
            }
            if (!loc.InHorDistOf(comp.parent.Position, HomesteadSettings.MaxYardDistance))
            {
                return false;
            }
            CompHouseMarker other = Map.GetComponent<HouseManager>()?.GetHouseAt(loc);
            if (other != null && other != comp)
            {
                return false;
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (remove)
            {
                comp.yardCells.Remove(c);
            }
            else
            {
                comp.yardCells.Add(c);
            }
            comp.Manager?.SetDirty();
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            GenUI.RenderMouseoverBracket();
            comp.EnsureCacheFresh();
            if (comp.cachedInteriorCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(new List<IntVec3>(comp.cachedInteriorCells), Color.white);
            }
            if (comp.yardCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(comp.yardCells, Color.green);
            }
        }
    }
}
