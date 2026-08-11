using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Homestead
{
    public class HomesteadMod : Mod
    {
        public static HomesteadSettings Settings;

        public HomesteadMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HomesteadSettings>();
        }

        public override string SettingsCategory() => "Homestead";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Homestead_SettingsHomeless".Translate(), ref Settings.enableHomelessThought,
                "Homestead_SettingsHomelessDesc".Translate());
            listing.CheckboxLabeled("Homestead_SettingsGuests".Translate(), ref Settings.enableGuestVisits,
                "Homestead_SettingsGuestsDesc".Translate());
            listing.CheckboxLabeled("Homestead_SettingsAvoidance".Translate(), ref Settings.enableAvoidance,
                "Homestead_SettingsAvoidanceDesc".Translate());
            listing.CheckboxLabeled("Homestead_SettingsReplaceBedroom".Translate(), ref Settings.replaceBedroomThoughts,
                "Homestead_SettingsReplaceBedroomDesc".Translate());

            listing.Gap();
            listing.Label("Homestead_SettingsMoodScale".Translate(Mathf.RoundToInt(Settings.moodScale * 100f)),
                tooltip: "Homestead_SettingsMoodScaleDesc".Translate());
            Settings.moodScale = listing.Slider(Settings.moodScale, 0.25f, 2f);

            listing.Label("Homestead_SettingsClaimDepth".Translate(Settings.claimDepth),
                tooltip: "Homestead_SettingsClaimDepthDesc".Translate());
            Settings.claimDepth = Mathf.RoundToInt(listing.Slider(Settings.claimDepth, 0f, 5f));

            listing.Label("Homestead_SettingsMaxRoomCells".Translate(Settings.maxRoomCells),
                tooltip: "Homestead_SettingsMaxRoomCellsDesc".Translate());
            Settings.maxRoomCells = Mathf.RoundToInt(listing.Slider(Settings.maxRoomCells, 50f, 600f));

            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            MoodScaler.Apply();
        }
    }

    // Rescales the baseMoodEffect of every Homestead thought stage by the settings slider.
    public static class MoodScaler
    {
        private static readonly Dictionary<ThoughtDef, float[]> originals = new Dictionary<ThoughtDef, float[]>();

        public static void Apply()
        {
            float scale = HomesteadMod.Settings?.moodScale ?? 1f;
            foreach (ThoughtDef def in DefDatabase<ThoughtDef>.AllDefsListForReading)
            {
                if (!def.defName.StartsWith("Homestead_") || def.stages == null)
                {
                    continue;
                }
                if (!originals.TryGetValue(def, out float[] bases))
                {
                    bases = new float[def.stages.Count];
                    for (int i = 0; i < def.stages.Count; i++)
                    {
                        bases[i] = def.stages[i]?.baseMoodEffect ?? 0f;
                    }
                    originals[def] = bases;
                }
                for (int i = 0; i < def.stages.Count; i++)
                {
                    if (def.stages[i] != null)
                    {
                        def.stages[i].baseMoodEffect = bases[i] * scale;
                    }
                }
            }
        }
    }
}
