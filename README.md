# Homestead

A RimWorld 1.6 mod that gives colonists **houses**, not just bedrooms.

## What it does

- **Welcome mat** (Furniture category, no research, stuffable; internally still
  `Homestead_HouseMarker` for save compatibility): a flat, walkable mat — lay it
  inside a room, traditionally by the front door, and assign an owner. Their spouse/fiancé/lover and minor children move in
  automatically; the assign dialog lets you override anyone. Cohabitation follows
  the same ideoligion rules as bed sharing: unmarried partners whose precepts
  forbid sharing a bed (spouses-only lovin') won't auto-move-in, can't be
  assigned together, and skip the merge-households letter until they marry. The house covers the
  marker's room plus rooms connected through doors (configurable depth, capped by
  room size so it won't swallow dining halls), and an optional painted **yard**.
- **Whole-house mood**: housed pawns get a situational thought graded on the
  cell-weighted average impressiveness of their house (same bands as vanilla room
  impressiveness) and lose the vanilla "slept in bedroom/barracks" memories. A
  dirty house gives a stacking debuff; Ascetics ignore house impressiveness.
- **Home upkeep**: a new work type (between Hauling and Cleaning in the work tab)
  makes owners clean filth and repair damaged buildings inside their own house at
  high priority — even repairs outside the vanilla home area.
- **Privacy**: colonists won't wander into, clean, haul from, or use joy buildings
  inside someone else's house (firefighting, rescue, doctoring, drafting and
  player-forced jobs are untouched). Owners who are home get a −3 "stranger in my
  house" memory when an uninvited colonist hangs around.
- **Homeless**: once the colony is established (any owned house exists, or 60 days
  have passed), colonists without a house get −2 after a 7-day grace period,
  worsening to −4 after 22 days. Kids under 13 and quest lodgers are exempt.
- **Living there**: relaxing in your own house grants a small "relaxed at home"
  buff; colonists occasionally visit a friend's home for social recreation, giving
  both sides a mood bonus. Couples living apart trigger a one-time suggestion
  letter to merge households.
- **Family under one roof**: sharing a house with a love partner, child or parent
  gives +3; partner *and* child together gives +4.
- **Sleep better at home**: resting inside your own house multiplies rest
  effectiveness (slider, default +10%).
- **Palatial ceiling**: an extra house-impressiveness stage (+9) above vanilla's
  top band, at average impressiveness ≥ 360 — houses can out-buff any bedroom.
- **Empty homes attract settlers**: a functional, ownerless house containing a
  free colonist bed rolls an MTB-12-day chance (per empty house, 6-day cooldown,
  only while storyteller `PopulationIntent > 0`) to spawn a Villager who walks in,
  claims the house and joins the colony.
- **Dubs Bad Hygiene integration** (soft, full or Lite; detected via
  `AccessTools.TypeByName` on `Building_BaseToilet` etc., no assembly reference):
  housed pawns want a toilet (−2 without) and washing fixture (−1 without, −3
  with neither) in their house, +2 with both; the inspect string gains a
  Bathroom line. Water troughs/pet bowls don't count as washing.
- **House tab & naming**: houses auto-name from the first owner's surname, are
  renameable, and the House inspector tab shows household, rooms, impressiveness,
  cleanliness and current mood contribution.
- **Mod settings**: toggles for homeless debuff, guest visits, privacy avoidance
  and bedroom-thought replacement; sliders for mood scale, room claim depth and
  max claimed room size.

## Building

```sh
cd Source && dotnet build
```

Compiles against the local Steam install (paths in `Homestead.csproj`) and the
Harmony workshop DLL; output lands in `Assemblies/`.

Textures are generated: `python3 tools/make_textures.py` (requires Pillow).

## Implementation notes

- `HouseManager` (MapComponent) claims rooms with a multi-source BFS from every
  marker through door connections — nearest marker wins, ties go to the older
  marker; rooms that are outdoors, oversized, contain another marker, or contain
  beds owned by non-members are never claimed. Caches rebuild at most every 250
  ticks or when dirtied.
- Harmony patches (all soft, with graceful skip-if-missing): suppress
  `SleptInBedroom`/`SleptInBarracks` memories for housed pawns; postfix
  `RCellFinder.CanWanderToCell`, `WorkGiver_CleanFilth.HasJobOnThing`,
  `HaulAIUtility.PawnCanAutomaticallyHaulFast_NewTemp` and
  `JoyGiver_InteractBuilding.CanInteractWith` to keep non-members out.
- Safe to add mid-save. Removing mid-save produces the usual one-time scribe
  errors.
