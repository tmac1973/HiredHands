# Phase 08 — Woodcutter and Miner behaviours

**Depends on:** 04 (effects framework, shared boosts: Strong swing, Pack mule, Strong back, Masterwork, Wide range,
Swift) · **Enables:** 12

## Goal
The gatherers' behaviour and yield enhancements. Woodcutter: Forester, Seed saver, Clean cut, Lumberjack, Fine eye,
Timber!, Master woodsman, Quiet work. Miner: Deep digger, Prospector, Rock breaker, Rich veins, Quiet work, Tireless,
Gem eye, Master miner, Heavy hauler.

## Files touched
- `src/VikingsForHire/Hirelings/Work/HarvestYield.cs` — new: while a hireling's own hit is being applied (the existing
  `HarvestPatches.Direct` window, extended to carry the hireling), a `DropTable.GetDropList` postfix multiplies each
  dropped stack by its yield bonus (fractions by chance): Lumberjack on logs, Master woodsman on everything it chops; Rich veins and
  Master miner on ore deposits; Fine eye on fine and core wood; plus Seed saver's seed and Gem eye's bonus item.
- `src/VikingsForHire/Hirelings/Work/HarvestPatches.cs` — the `Direct` window records the hitting hireling.
- `src/VikingsForHire/Hirelings/Work/WoodcutterProfile.cs` — Fine eye prefers trees whose best drop is FineWood or
  RoundLog (core wood); Clean cut × 1.3 swing damage on logs and stumps; Timber!.
- `src/VikingsForHire/Hirelings/Work/Trees/PlantTreesBehaviour.cs` — Forester halves the planting interval.
- `src/VikingsForHire/Hirelings/Work/MinerProfile.cs` — `DigDepth` from 1.5 to `DeepDigger.depth` (2.5) with Deep digger;
  Prospector: ore-bearing targets always ranked before stone-only ones (even nearer stone), and its search radius × 1.3
  for them; Rock breaker × 1.3 swing damage on rocks with no ore.
- `src/VikingsForHire/Hirelings/Work/GatherBehaviour.cs` — Quiet work: no gathering noise for this hireling
  (`GatheringMakesNoise` checks `Enh.Has(QuietWork)`).
- `src/VikingsForHire/Hirelings/Hireling.cs` — `CargoWeight()`: the cargo's weight with Heavy hauler's ore factor
  (`HeavyHauler.oreWeight` 0.5 for items whose prefab ends in `Ore` or `OreNew`); `CargoFull`, `CargoWeightRoom`
  and the cargo window label use it; Tireless raises the "full" threshold to × 1.25 of the limit for mining trips.
- `src/VikingsForHire/Hirelings/CargoGate.cs` — weight checks use `CargoWeight()`.
- `src/VikingsForHire/Core/Enhancements/GemTable.cs` — new: Gem eye's bonus items by biome (Meadows/Black Forest: Flint,
  Amber; Swamp: Amber, AmberPearl; Mountain: Crystal, Obsidian; Plains: AmberPearl, Ruby; Mistlands and later: Ruby).
- `src/VikingsForHire/Testing/FixturesWork.cs` — fixture `bury <tag> <depth>` (moves a tagged deposit down so its top is
  that far under the ground); fixture `tree_at <prefab> <tag> <of tag> <metres>` (a tree that many metres from a tagged tree,
  on the side away from the board, where it falls); check field `work_target` (prefab name of its current work target).
- `test/alias_vfh.yaml` — rows VFH-ENH-L1 … L8 (woodcutter), VFH-ENH-M1 … M8 (miner). Trees and deposits come from the
  existing `trees` and `deposits` fixtures.

## Steps
1. **Yield** (one hook, many picks): Lumberjack (+0.15 wood), Master woodsman (+0.15 everything it chops; its +0.15
   swing speed is applied through phase 04's `GatherMult`), Fine eye (+0.10 FineWood/RoundLog), Rich veins (+0.15 ore),
   Master miner (+0.15 all, swing +0.15 as above).
2. **Seed saver** (L4 A): each tree it fells drops one seed of its kind (beech seeds, birch seeds, acorn, fir or pine
   cone; the tree patch code's tree → seed map).
3. **Forester** (L3 A), **Clean cut** (L4 B), **Fine eye** targeting (L6 A), **Timber!** (L7 A: after felling, the nearest
   tree within `Timber.range` (4 m) in the fall direction whose health is ≤ `Timber.maxHealth` (100) is felled too).
4. **Quiet work** (Woodcutter L8 B, Miner L6 A).
5. **Deep digger** (L3 A), **Prospector** (L4 A), **Rock breaker** (L4 B), **Tireless** (L7 A), **Gem eye** (L7 B: chance
   `GemEye.chance` 0.10 per chunk broken), **Heavy hauler** (L8 B).
6. Rows: L1 lumberjack (a level 8 woodcutter fells 3 beeches: wood delivered ≥ 1.1 × a plain one's, same trees;
   fixture `trees` gives identical trees); L2 seed saver (`deposited` BeechSeeds > 0 with no patch); L3 forester (a patch
   gets planted twice as fast: `patch_saplings` after 120 s ≥ 1.8 × a plain woodcutter's in the same row); L4 fine eye (`trees Beech1 1 15` and `trees Birch1 1 18`: its first
   `work_target == Birch1`, a plain woodcutter's `Beech1`);
   L5 timber (`trees Beech1 1 15 T`, `tree_at Beech_small1 S T 3`: after
   it fells T, S is gone too); L6 quiet (the VFH-NOISE-1 setup, a greydwarf 45 m off while it chops:
   `enemy_target G == none` for 60 s, where VFH-NOISE-1's woodcutter is attacked); L7 clean cut (the logs
   of three felled beeches cleared in ≤ 0.8 × the time a plain level 8 woodcutter takes in the same row); L8 master woodsman
   (wood delivered ≥ 1.1 × plain from the same three trees);
   M1 deep digger (`deposits rock4_copper 1 30 D`, `bury D 2`: mined with it,
   `deposited` copper stays 0 for a plain miner in 3 minutes); M2 rock breaker (a plain rock broken in ≤ 0.8 × the time a
   plain level 8 miner takes in the same row); M3 rich veins (ore delivered ≥ 1.1 × plain); M4 tireless (`hireling posted cargo_weight` reaches more than its limit, at most
   × 1.25, before it goes to deliver); M5 gem eye (`data_set Miner GemEye.chance 1`: after a copper deposit, `hireling
   posted cargo.Flint` or `cargo.Amber` > 0; `data_reset`); M6 heavy hauler (a level 8 miner: `cargo_put SilverOre 3` puts in twice
   as much as for a plain level 8 miner in the same row); M7 prospector (a copper deposit at 1.2 × the work radius is
   mined with it and not without); M8 quiet work (as L6, for a miner).

## Build gate
- Compile check; unit tests (GemTable, the yield maths in Core).
- In game: rows L1-L8, M1-M8 pass; regression `work1 work3 tier1 keep1 keep2 deliver1 deliver2 gather1 order1 tree_patch
  noise1 cargo_weight` passes.

## Test plan
Rows above; regression of gathering, delivery, tree patches and cargo.

## Commit
`feat(enhancements): woodcutter and miner behaviours and yields (forester, seed saver, clean cut, lumberjack, fine eye, timber, quiet work, deep digger, prospector, rock breaker, rich veins, tireless, gem eye, heavy hauler, masters); rows VFH-ENH-L1..L8, M1..M8`

## Rollback
Gated by `Enh.Has`; the drop-table hook only acts inside a hireling's own hit, so reverting restores vanilla drops.
