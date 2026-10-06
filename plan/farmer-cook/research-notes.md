# Farmer & Cook research (Valheim internals + PlantEverything / PlantEasily)

Sources:
- Code: `ilspycmd` on the publicized `assembly_valheim.dll` (Plant, Pickable, Piece, Player, CookingStation, CraftingStation, StationExtension, Recipe, InventoryGui, Inventory, Heightmap, Game).
- **Prefab data is real, read from this install's game data**: `/games/SteamLibrary/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles/c4210710` (Unity 6000.0.75f1). I parsed it with a small script (`scratchpad/ufs.py` + `vh.py`, using system liblz4 and the type trees). Everything in the tables below comes from those prefabs unless it says **[knowledge]**.
- Mods: Advize-PlantEverything 1.21.3 and Advize-PlantEasily 2.3.0 from profile `1dotohsupermodded`, decompiled, plus their cfg files.

Biome bitmask (`Heightmap.Biome`): Meadows=1, Swamp=2, Mountain=4, BlackForest=8, Plains=16, AshLands=32, DeepNorth=64, Ocean=256, Mistlands=512.

**This build has Deep North farming content** (Kale, Oat, Poteitr, Lingonberry, Moose, Seal; cauldron extensions up to ext7; recipes at cauldron levels 6 and 7). Plan for it.

---

## 1. Vanilla planting

### Plant component (`public class Plant : SlowUpdate, Hoverable`)
Fields: `string m_name`, `float m_growTime` (10), `float m_growTimeMax` (2000), `GameObject[] m_grownPrefabs`, `float m_minScale/m_maxScale`, `float m_growRadius` (1), `float m_growRadiusVines`, `bool m_needCultivatedGround`, `bool m_destroyIfCantGrow`, `bool m_tolerateHeat`, `bool m_tolerateCold`, `Heightmap.Biome m_biome` (bitmask), `float m_attachDistance` (vines), private `Status m_status`, `ZNetView m_nview`, `int m_seed`.

`enum Status { Healthy, NoSun, NoSpace, WrongBiome, NotCultivated, NoAttachPiece, TooHot, TooCold }`. `Status GetStatus()` returns `m_status`.

How it works:
- `Awake`: on the owner, stores `ZDOVars.s_plantTime` (ticks of `ZNet.instance.GetTime()`) if not set; `ZDOVars.s_seed` from the ZDO uid.
- `SUpdate` (every 10 s, only while the zone is loaded): `UpdateHealth(TimeSincePlanted())`; if owner and `TimeSincePlanted() > GetGrowTime()` then `Grow()`.
- `float GetGrowTime()`: seeded random `Lerp(m_growTime, m_growTimeMax, rand(m_seed))`. Same value on every peer. Growth catches up when the zone reloads, because time is measured from `s_plantTime`.
- `GameObject Grow()`: if not Healthy, it destroys itself when `m_destroyIfCantGrow` (all crops have it), else returns null. Otherwise it instantiates a random `m_grownPrefabs[i]` at the same position (yaw ±11.25°), sets the scale, and calls `m_nview.Destroy()` on the sapling.
- `void UpdateHealth(double t)`: Healthy for the first 10 s, then checks in this order:
  1. `Heightmap.FindHeightmap(pos).GetBiome(pos) & m_biome == 0` gives **WrongBiome**.
  2. `m_needCultivatedGround && !heightmap.IsCultivated(pos)` gives **NotCultivated**.
  3. `!m_tolerateHeat && biome==AshLands && !ShieldGenerator.IsInsideShield(pos)` gives **TooHot**.
  4. `!m_tolerateCold && (biome==DeepNorth || biome==Mountain) && !IsInsideShield` gives **TooCold**.
  5. `HaveRoof()` gives **NoSun**: `Physics.Raycast(pos, Vector3.up, 100f, mask Default|static_solid|piece)`.
  6. `!HaveGrowSpace()` gives **NoSpace**: `Physics.OverlapSphereNonAlloc(pos, m_growRadius, s_colliders, mask Default|static_solid|Default_small|piece|piece_nonsolid)`. **Any collider that isn't a Plant blocks, and so does any *other Healthy* Plant.** Grown Pickables, rocks, pieces and players all block. Then, if `m_growRadiusVines>0`, any `Vine` within that radius blocks.
  7. Vines only: `GetClosestAttachPosRot` fails gives NoAttachPiece.
  8. Otherwise Healthy.
- Status is only computed where the sapling is loaded and simulated. Read it with `plant.GetStatus()`. It is not stored in the ZDO.
- Helpers (publicized): `double TimeSincePlanted()`, `float GetGrowTime()`, `bool HaveRoof()`, `bool HaveGrowSpace()`.

### Cultivated ground
- `bool Heightmap.IsCultivated(Vector3 worldPos)` returns `m_paintMask.GetPixel(x,y).g > 0.5f`. `float GetCultivationMask(pos)` gives the raw g value. `bool IsCleared(pos)` is r, g or b > 0.5.
- Get the heightmap with `Heightmap.FindHeightmap(Vector3)` (static). For the biome, `Heightmap.FindBiome(Vector3)` (static) or `hm.GetBiome(pos)`.
- The cultivator paints with a `TerrainOp` (`cultivate_v2`) that writes the paint mask into `TerrainComp`. Vanilla `Plant.UpdateHealth` calls `IsCultivated` on whoever owns the sapling, including a dedicated server, so it works server-side whenever the zone's heightmap exists. To find empty soil, sample `IsCultivated` on a grid inside the work radius. Get Y with `ZoneSystem.instance.GetGroundHeight(pos)`.

### Piece (sapling) fields relevant to planting
`Piece.m_cultivatedGroundOnly` (true on all crop saplings), `m_onlyInBiome` (None on all vanilla saplings), `m_groundOnly`, `m_vegetationGroundOnly`, `Piece.Requirement[] m_resources` (the seed). `class Requirement { ItemDrop m_resItem; int m_amount=1; int m_amountPerLevel; bool m_recover; int GetAmount(int qualityLevel) }`. `GetAmount(0 or 1)` returns `m_amount`.
`Piece.m_harvest / m_harvestRadius / m_harvestRadiusMaxLevel` are all 0 on vanilla saplings. **PlantEasily writes its per-pickable spacing into `m_harvestRadius` on pickable prefabs** (see §5).

### What player placement checks (`Player.UpdatePlacementGhost`)
Relevant to saplings:
- `m_cultivatedGroundOnly && (heightmap==null || !heightmap.IsCultivated(point))` gives `PlacementStatus.NeedCultivated`.
- `m_onlyInBiome != None && (Heightmap.FindBiome(pos) & m_onlyInBiome)==0` gives WrongBiome.
- `Location.IsInsideNoBuildLocation`, `PrivateArea.CheckAccess` (ward), players blocking, `m_noInWater`, `m_notOnTiltingSurface`, and so on.
- **Placement doesn't check `Plant.m_biome`, roof or grow space.** Those only show up later as Plant status (the sapling then dies, because `m_destroyIfCantGrow`). The Farmer should pre-check them itself: biome & `plant.m_biome`, `IsCultivated`, upward raycast, overlap sphere at `m_growRadius`, cold/heat with `ShieldGenerator.IsInsideShield`.

### `Player.PlacePiece(Piece piece, Vector3 pos, Quaternion rot, bool doAttack=true, bool cheated=false)`
It does this:
```
TerrainModifier.SetTriggerOnPlaced(true);
var go = Object.Instantiate(piece.gameObject, pos, rot);
TerrainModifier.SetTriggerOnPlaced(false);
go.GetComponent<Piece>()?.SetCreator(GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
go.GetComponent<WearNTear>()?.OnPlaced(); ... IPlaced.OnPlaced() on components ...
piece.m_placeEffect.Create(pos, rot, go.transform, 1f, -1, GetZDOID());
```
The cost is taken separately by the caller: `ConsumeResources(selectedPiece.m_resources, 0)`, which does `m_inventory.RemoveItem(req.m_resItem.m_itemData.m_shared.m_name, req.GetAmount(0))`, unless `ZoneSystem.GetGlobalKey(piece.FreeBuildKey())`.

**Placing a sapling from code (no player)**, copying what PlantEasily's `PlacementController.PlacePiece` does:
```
TerrainModifier.SetTriggerOnPlaced(true);
var go = Object.Instantiate(saplingPrefab, pos, Quaternion.Euler(0, 22.5f*Random.Range(0,16), 0));
TerrainModifier.SetTriggerOnPlaced(false);
// optional: piece.SetCreator(playerId, platformUserId) -- needs ZNet.World.m_playerHistory lookup; creator 0 is fine for a Plant
piece.m_placeEffect.Create(pos, rot, go.transform);
container.GetInventory().RemoveItem(req.m_resItem.m_itemData.m_shared.m_name, req.m_amount);
```
- The instantiating peer owns the new ZDO, so `Plant.Awake` writes `s_plantTime` there.
- `Piece.SetCreator(long uid, PlatformUserID)` only acts if `m_nview.IsOwner() && GetCreator()==0`.
- PlantEverything postfixes `SetCreator`. Its `ResourcesSpawnEmpty` and `PlaceAnywhere` hooks only fire when SetCreator is called, which matters only for PE pieces. The Farmer doesn't plant those.

### Spacing
- All vanilla crops have `m_growRadius = 0.5` (magecap 0.8; tree saplings 2–3; vines 0.5 + `m_growRadiusVines` 1.8). The sphere check uses the plant's own radius against any collider. Grown crop Pickables have colliders, so a ripe neighbour within 0.5 m blocks.
- PlantEasily's crop spacing is `m_growRadius * 2 + ExtraCropSpacing`, which is 1.0 m for vanilla crops and 1.6 m for magecap (see §5).

---

## 2. Harvest: Pickable (`public class Pickable : MonoBehaviour, Hoverable, Interactable`)

Fields:
- Item and yield: `GameObject m_itemPrefab`, `int m_amount=1`, `int m_minAmountScaled=1`, `bool m_dontScale`, `DropTable m_extraDrops`.
- Respawn: `float m_respawnTimeMinutes`, `float m_respawnTimeInitMin/Max`, `GameObject m_hideWhenPicked`, `bool m_defaultPicked`.
- Other: `bool m_harvestable` (true on crops), `Skills.SkillType m_pickRaiseSkill` (106 = Farming on crops and berries), `float m_maxLevelBonusChance=0.25`, `int m_bonusYieldAmount=1`, `bool m_tarPreventsPicking`, `float m_aggravateRange`, `SpawnCheck m_spawnCheck`.
- Private: `ZNetView m_nview`, `bool m_picked`, `int m_enabled`, `long m_pickedTime`.

ZDO: `ZDOVars.s_picked` (bool), `ZDOVars.s_pickedTime` (long ticks), `ZDOVars.s_enabled`.

Flow:
- `bool Interact(Humanoid character, bool repeat, bool alt)`: the skill raise and bonus roll only happen if `character is Player`. Then it calls `m_nview.InvokeRPC("RPC_Pick", bonus)`.
- `void RPC_Pick(long sender, int bonus)` runs on the owner and only if not picked:
  - It calls `m_pickEffector.Create(..., Player.m_localPlayer.GetZDOID())`. **That throws a NullReferenceException on a dedicated server**, where `m_localPlayer` is null, so don't route Farmer picks through `Interact`/`RPC_Pick` when the server simulates the worker.
  - Amount: `num = m_dontScale ? m_amount : Max(m_minAmountScaled, Game.instance.ScaleDrops(m_itemPrefab, m_amount)) + bonus`. It drops `num` single items, plus `m_extraDrops.GetDropListItems()`, as ItemDrops at the spot (`Drop()` gives them `linearVelocity` 4 up).
  - Then `InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true)`.
- `void SetPicked(bool picked)`, on the owner:
  - If `m_respawnTimeMinutes > 0 || m_hideWhenPicked != null` (bushes, mushrooms, PE pickables), it stores `s_picked` and `s_pickedTime = now`.
  - **Otherwise it calls `m_nview.Destroy()`**. That's every grown crop (`Pickable_Carrot` etc., `m_respawnTimeMinutes=0`, no `m_hideWhenPicked`). There is no `m_destroyOnPick` field; destruction follows from these two values.
- `Awake` also destroys an old picked crop (respawn 0, no hide object, `s_picked` true).
- Respawn: `InvokeRepeating("UpdateRespawn", …, 60f)` on the owner, then `ShouldRespawn()`: `(now - s_pickedTime).TotalMinutes > m_respawnTimeMinutes`, then `RPC_SetPicked(false)`.
- Ripe test: `bool GetPicked()` (`m_picked`) and `GetEnabled==1`, or `zdo.GetBool(ZDOVars.s_picked, m_defaultPicked)`. `bool CanBePicked()` has odd logic for hide objects, so prefer `!GetPicked()`.

**Suggested Farmer harvest (works on server or client owner):** compute the yield yourself:
- main item: `m_dontScale ? m_amount : Mathf.Max(m_minAmountScaled, Game.instance.ScaleDrops(m_itemPrefab, m_amount))`
- extras: `m_extraDrops.GetDropListItems()` (each has `m_dropPrefab`, `m_stack`)

Add the items to the hireling's inventory or the chest, play `m_pickEffector.Create(pos, Quaternion.identity)` without the player zdoid, then call `m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true)`. The owner then destroys the crop or timestamps the bush. Check `GetPicked()` first so you don't double-pick. (The Farming bonus yield is player-only and can be skipped.)

**Yield of a growing Plant:** `plant.m_grownPrefabs[i].GetComponent<Pickable>()` gives `m_itemPrefab`, `m_amount` (scaled as above) and `m_extraDrops`. DropTable fields: `m_dropMin`, `m_dropMax`, `m_dropChance`, `m_oneOfEach`, `List<DropData> m_drops` (`m_item`, `m_stackMin`, `m_stackMax`, `m_weight`). An empty `m_drops` drops nothing even when min/max is 1. For a deterministic count, take `m_amount` plus the expected extras (see the table). Time to ripe: `plant.GetGrowTime() - plant.TimeSincePlanted()`.

---

## 3. Vanilla cultivator crops (read from the prefabs; `_CultivatorPieceTable` order)

Cultivator piece table: cultivate_v2, replant_v2, sapling_turnip, sapling_seedturnip, sapling_onion, sapling_seedonion, sapling_carrot, sapling_seedcarrot, sapling_barley, sapling_flax, sapling_jotunpuffs, sapling_magecap, sapling_oat, sapling_poteitr, sapling_Kale, sapling_seedkale, Beech_Sapling, Birch_Sapling, Oak_Sapling, FirTree_Sapling, PineTree_Sapling, FirTree_big_Sapling, VineAsh_sapling, VineGreen_sapling.

All crop saplings share these values: `m_growTime 4000`, `m_growTimeMax 5000` s; `m_growRadius 0.5` (magecap 0.8); `m_needCultivatedGround` and `Piece.m_cultivatedGroundOnly` true; `m_destroyIfCantGrow` true; `m_tolerateHeat/Cold` false; seed cost 1. Grown pickables: `m_respawnTimeMinutes 0` (destroyed on pick), `m_harvestable` true, skill Farming.

| Sapling prefab | Consumes | Grows into | Yields (m_amount + extra drops) | Plant.m_biome |
|---|---|---|---|---|
| sapling_carrot | CarrotSeeds ×1 | Pickable_Carrot | Carrot ×1 | Meadows, BlackForest, Plains, AshLands (57) |
| sapling_seedcarrot | Carrot ×1 | Pickable_SeedCarrot | CarrotSeeds ×3 | 57 |
| sapling_turnip | TurnipSeeds ×1 | Pickable_Turnip | Turnip ×1 | Meadows, Swamp, BlackForest, Plains, AshLands, Mistlands (571) |
| sapling_seedturnip | Turnip ×1 | Pickable_SeedTurnip | TurnipSeeds ×3 | 571 |
| sapling_onion | OnionSeeds ×1 | Pickable_Onion | Onion ×1 | 57 |
| sapling_seedonion | Onion ×1 | Pickable_SeedOnion | OnionSeeds ×3 | 57 |
| sapling_barley | Barley ×1 | Pickable_Barley | Barley ×2 (m_minAmountScaled 2) | Plains only (16) |
| sapling_flax | Flax ×1 | Pickable_Flax | Flax ×2 (minScaled 2) | Plains only (16) |
| sapling_jotunpuffs | MushroomJotunPuffs ×1 | Pickable_Mushroom_JotunPuffs | JotunPuffs ×1 + extra 2 = **3** | Mistlands only (512) |
| sapling_magecap (radius 0.8) | MushroomMagecap ×1 | Pickable_Mushroom_Magecap | Magecap ×1 + extra 2 = **3** | Mistlands only (512) |
| sapling_oat | OatSeeds ×1 | Pickable_Oat | **OatSeeds ×3** (the windmill turns OatSeeds into Oat, then Oat into OatFlour) | 57 |
| sapling_poteitr | PoteitrSeeds ×1 | Pickable_Poteitr | Poteitr ×3 + PoteitrSeeds 1–2 (one extra roll) | 57 |
| sapling_Kale | KaleSeeds ×1 | Pickable_Kale | Kale ×3 | 57 |
| sapling_seedkale | **KaleSeeds ×1** (as the data says; not Kale) | Pickable_SeedKale | KaleSeeds ×3 | 57 |
| VineAsh_sapling | VineberrySeeds ×1 | VineAsh (a vine, not a crop) | Vineberry ×3, respawns every 200 min, extra VineberrySeeds 1–3 at 20% | wide; tolerateHeat; needs cultivated ground and a build piece to climb (attach 1.8) |
| VineGreen_sapling | VineGreenSeeds ×1 | VineGreen | Vineberry ×3, respawns 200 min | wide; tolerateHeat |

Notes:
- No crop has `tolerateCold`, so none grow in the Mountain or Deep North without a shield generator (`TooCold`). AshLands is in the 57 mask, but `TooHot` applies there without a shield. Deep North crops (Kale, Oat, Poteitr) therefore need a shield in the Deep North, or are grown in Meadows/BF/Plains.
- Self-sustaining crops (replant from their own harvest): barley, flax, jotunpuffs, magecap (×3), oat seeds (×3), poteitr (makes seeds), kale seeds (×3). Carrot, turnip, onion and kale alternate between a "seed" sapling and a "produce" sapling.
- Smoke puff and fiddlehead are **not** plantable in vanilla; they come only as wild Pickables or through PlantEverything.
- Trees (Beech/Birch/Oak/Fir/Pine/FirBig saplings) grow TreeBase objects with no Pickable. That's a non-goal.

Vanilla wild or regrowing pickables (no Piece component in vanilla):

| Prefab | Item | Amount | Respawn (min) | Hide object |
|---|---|---|---|---|
| RaspberryBush | Raspberry | 1 | 300 | Berrys |
| BlueberryBush | Blueberries | 1 | 300 | Berrys |
| CloudberryBush | Cloudberry | 1 | 300 | Berrys |
| LingonberryBush | Lingonberry | 1 | 300 | Berries |
| Pickable_Mushroom / _yellow / _blue | Mushroom / MushroomYellow / MushroomBlue | 1 | 240 | visual |
| Pickable_Thistle | Thistle | 1 | 240 | visual |
| Pickable_Dandelion | Dandelion | 1 | 240 | visual |
| Pickable_SmokePuff | MushroomSmokePuff | 1 | 240 | visual |
| Pickable_Fiddlehead | Fiddleheadfern | 1 (+1 extra) | 300 | visual |
| Pickable_Branch | Wood | 1 | 240 | model |
| Pickable_Flint | Flint | 1 | 240 | model |
| Pickable_Stone | Stone | 1 | 0 (destroyed) | none |
| Pickable_Barley_Wild / Pickable_Flax_Wild | Barley / Flax | 2 | 0 | none |

---

## 4. PlantEverything (advize.PlantEverything 1.21.3)

- `[BepInPlugin("advize.PlantEverything", "PlantEverything", "1.21.3")]`. Detect it with `BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("advize.PlantEverything")`. It uses ServerSync, so config is server-locked (`LockConfiguration = true`).
- **Its regrowing plants are not Plant saplings.** PE adds (or gets) a `Piece` component on the vanilla Pickable prefab itself, in `PluginUtils.CreatePiece`/`GetOrAddPieceComponent`, and puts it in the cultivator table. The player places the Pickable directly. It is ripe at once, unless `ResourcesSpawnEmpty`, which invokes `RPC_SetPicked(true)` in a `Piece.SetCreator` postfix. After that it regrows with `m_respawnTimeMinutes` (the vanilla respawn system).
- Pieces it adds (from `StaticContent.GeneratePieceRefs`):
  - **Berries**: RaspberryBush, BlueberryBush, CloudberryBush, LingonberryBush.
  - **Mushrooms and flowers**: Pickable_Mushroom, Pickable_Mushroom_yellow, Pickable_Mushroom_blue, Pickable_Thistle, Pickable_Dandelion, Pickable_SmokePuff, Pickable_Fiddlehead (PE clears the fiddlehead's extra drops).
  - **Debris** (`EnableDebris`): Pickable_Branch, Pickable_Stone, Pickable_Flint.
  - **Misc flora** (`EnableMiscFlora`, decorative with no Pickable): Beech_small1, FirTree_small, FirTree_small_dead, Bush01, Bush01_heath, Bush02_en, shrub_2, shrub_2_heath, YggaShoot_small1, vines, FernAshlands.
  - **Custom tree saplings** (Plant to TreeBase): Ancient_Sapling, Ygga_Sapling, Autumn_Birch_Sapling, Ashwood_Sapling.
  - Custom visual prefabs `Pickable_X_Picked` (shown as a "spawner" when picked, `ShowPickableSpawners`).
  - Optional user `ExtraResources` (a JSON file) can turn any prefab into a piece.
- PE Piece `m_name` is `"$pe" + Name + "Name"`, e.g. `$pePickableMushroomName`, `$peRaspberryBushName`. `PluginUtils.IsModdedPrefabOrSapling(s)` is `s.StartsWith("$pe") || s.EndsWith("_sapling")`.
  - A PE-placed bush: `GetComponent<Piece>()?.GetCreator() != 0`. With PE installed, *world-generated* bushes also carry the Piece, but with creator 0.
  - PE sets `pickable.m_amount = <X>Return` and `m_respawnTimeMinutes = <X>RespawnTime` on the prefab, so both wild and planted bushes use the config values.
- It also changes vanilla crops (`InitCrops`): `m_biome` becomes all biomes unless `EnforceBiomesVanilla` (default true). With `EnableCropOverrides` it also changes cost, return, grow times, grow radius and cultivation. `CropsRequireSunlight=false` and `CropsRequireGrowthSpace=false` patch `Plant.HaveRoof`/`HaveGrowSpace` for `$piece_sapling*`. `PlantsRequireShielding=false` sets tolerateCold/Heat. **So read values from the live prefab (`ZNetScene.GetPrefab(name)`) at runtime, never hard-code them.**
- Config file `advize.PlantEverything.cfg` sections and keys (values in Tim's profile):
  - [General]: LockConfiguration=true, ShowPickableSpawners=true, EnableMiscFlora=true, EnableDebris=true, EnableExtraResources=false, SnappableVines, EnableLocalization, Language, DisabledResourceNames.
  - [Difficulty]: RequireCultivation=false (PE pickables need cultivated ground only if true), PlaceAnywhere=false, EnforceBiomes=false, EnforceBiomesVanilla=true, PlantsRequireShielding=true, CanRemoveFlora, RecoverResources, ResourcesSpawnEmpty=false, EnemiesTargetPieces.
  - [Berries]: {Raspberry,Blueberry,Cloudberry,Lingonberry}{Cost=5, RespawnTime=300, Return=1}.
  - [Mushrooms]: {Mushroom,YellowMushroom,BlueMushroom,Smokepuff}{Cost=5, RespawnTime=240, Return=1}.
  - [Flowers]: Thistle/Dandelion Cost 5, Respawn 240, Return 1; Fiddlehead Cost 15, Respawn 300, Return 3.
  - [Debris]: PickableBranch/Flint Cost 5, Respawn 240; PickableStone Cost 1, Respawn 0.
  - [Crops]: EnableCropOverrides=false, OverrideModdedCrops, CropMinScale, CropMaxScale, CropGrowTimeMin=4000, CropGrowTimeMax=5000, CropGrowRadius=0.5, CropsRequireCultivation=true, CropsRequireSunlight=true, CropsRequireGrowthSpace=true, EnemiesTargetCrops, and {Barley,Carrot,Flax,Onion,SeedCarrot,SeedOnion,SeedTurnip,Turnip,Magecap,JotunPuffs,Oat,Poteitr,Kale,SeedKale}{Cost,Return}.
  - [Saplings]: per-tree growth time, radius and scale.
  - [Seeds]: EnableSeedOverrides and tree seed drops.
  - [Vines]: EnableVineOverrides, VineAttachDistance, VineGrowRadius, VineGrowthTime, VineBerryRespawnTime, VineBerryReturn.
  - [UI]: EnablePickableTimers, EnablePlantTimers, GrowthAsPercentage.
- For the Farmer: treat any `Pickable` with `m_respawnTimeMinutes > 0` (bush or mushroom) as "regrowing, harvest only". Treat `m_respawnTimeMinutes == 0` together with a sapling that grows into it as a crop.

---

## 5. PlantEasily (advize.PlantEasily 2.3.0)

- `[BepInPlugin("advize.PlantEasily", "PlantEasily", "2.3.0")]`, with a BepInDependency (it didn't decode; probably the ConditionalConfigSync API, unverified). **On a dedicated server it applies no Harmony patches** (`graphicsDeviceType == Null`). The config is still bound there, apart from the per-pickable spacing entries, which are bound from a ZNetScene patch.
- **Grid spacing** (`ModUtils.GetPieceSpacing(GameObject)`):
  - If the piece has a `Plant`:
    - tree (TreeBase in itself or its grown prefabs): `m_growRadius*2.2 + ExtraSaplingSpacing`
    - crop: `m_growRadius*2 + ExtraCropSpacing`
    - crop with `MinimizeGridSpacing=true`: `m_growRadius*1.1 + max CapsuleCollider radius + ExtraCropSpacing`
  - Otherwise (a pickable piece): `piece.m_harvestRadius > 0 ? piece.m_harvestRadius : DefaultGridSpacing`.
  - `PickableDB.InitPickableSpacingConfig` writes each `"<Prefab> GridSpacing"` value into **`Piece.m_harvestRadius` on that pickable prefab**.
- Rows and columns: [General] Rows / Columns (the grid size the player places), RandomizeRotation (yaw `22.5*Random.Range(0,16)`), EnableScatter, PositionScatterRadius, RotationScatterAngle.
- [Grid]: GloballyAlignGridDirections (true: rows line up with world X/Z), MinimizeGridSpacing, GridSnappingStyle (Intelligent/Legacy), ForceAltPlacement, PreferCardinalSnapping, ExtraCropSpacing (0), ExtraSaplingSpacing (0). It snaps to existing grids by scanning neighbours at about `spacing ± 1%` (`CollisionScanner.ScanNeighbours`).
- [Pickables]: PreventOverlappingPlacements; DefaultGridSpacing=1; one `"<Prefab> GridSpacing"` key each: RaspberryBush 1.5, BlueberryBush 1.5, LingonberryBush 1.5, CloudberryBush 1, Pickable_Dandelion 0.75, Pickable_Thistle 0.75, Pickable_Mushroom*/_blue/_yellow 0.5, Pickable_SmokePuff 1, Pickable_Fiddlehead 1, Pickable_Branch/Flint/Stone 1, Placeable_Stone 1.
- [Harvesting]: EnableBulkHarvest=true, HarvestStyle (AllResources | LikeResources), **HarvestRadius=3**, ReplantOnHarvest=false. Bulk harvest is a prefix on `Player.Interact`: with the modifier key (KeyboardHarvestModifierKey = LeftShift) or alt, it runs `Physics.OverlapSphere(root.position, HarvestRadius, …)` and calls `Interact` on every Pickable/Beehive (Beehives only if the honey level is at least 1). Replant uses the local player's inventory.
- [Difficulty]: PreventPartialPlanting, PreventInvalidPlanting, UseStamina, UseDurability. [Performance]: MaxConcurrentPlacements, GhostUpdateBatchSize, BulkPlantingBatchSize. [Controls] and [UI] are cosmetic.
- **Reading its values from another mod:**
  ```
  if (Chainloader.PluginInfos.TryGetValue("advize.PlantEasily", out var pi)) {
      var cfg = pi.Instance.Config;                       // BepInEx.Configuration.ConfigFile
      cfg.TryGetEntry<float>("Grid", "ExtraCropSpacing", out var extra);
      cfg.TryGetEntry<bool>("Grid", "MinimizeGridSpacing", out var minimize);
      cfg.TryGetEntry<bool>("Grid", "GloballyAlignGridDirections", out var align);
      cfg.TryGetEntry<float>("Harvesting", "HarvestRadius", out var hr);
      cfg.TryGetEntry<float>("Pickables", "RaspberryBush GridSpacing", out var rb); // may be unbound on a dedicated server
  }
  ```
  On a client the easier route is to read the live `Piece.m_harvestRadius` of the pickable prefab. Crop spacing is just `plant.m_growRadius*2 + ExtraCropSpacing`. PlantEasily's config isn't server-synced in general (the ConditionalConfigSync bits are only the stamina and durability values), so **a dedicated server sees the server's own cfg file, or nothing if PlantEasily isn't installed there.** Fall back to `m_growRadius*2`.

---

## 6. Cooking stations (CookingStation)

`public class CookingStation : MonoBehaviour, Interactable, Hoverable, IHasHoverMenuExtended`:
- Conversions: `List<ItemConversion> m_conversion` (`ItemDrop m_from`, `ItemDrop m_to`, `float m_cookTime`), `List<ItemMessage> m_incompatibleItems`.
- Overcooking: `bool m_canOvercookItems`, `ItemDrop m_overCookedItem`.
- Layout: `Transform[] m_slots`, `Transform m_spawnPoint`, `float m_spawnForce`.
- Fire: `bool m_requireFire`, `Transform[] m_fireCheckPoints`, `float m_fireCheckRadius`.
- Fuel: `bool m_useFuel`, `bool m_useFueldWhileEmpty`, `ItemDrop m_fuelItem`, `int m_maxFuel`, `int m_secPerFuel`.
- Switches: `Switch m_addFoodSwitch`, `Switch m_addFuelSwitch` (the oven uses these, so `Interact()` returns false on it).
- Other: `Skills.SkillType m_skill` (Cooking 105), `bool m_canGiveBonusYield`.

| Prefab | Slots | Fire / fuel | Conversions |
|---|---|---|---|
| piece_cookingstation (spit) | 2 | requireFire, 1 fire check point | 25 s: RawMeat→CookedMeat, NeckTail→NeckTailGrilled, FishRaw→FishCooked, DeerMeat→CookedDeerMeat, WolfMeat→CookedWolfMeat, ChickenMeat→CookedChickenMeat, HareMeat→CookedHareMeat, BjornMeat→CookedBjornMeat. Incompatible ("too weak"): SerpentMeat, LoxMeat, AsksvinMeat, VoltureMeat, BoneMawSerpentMeat, BugMeat |
| piece_cookingstation_iron | 5 | requireFire, **3** fire check points (all must be in a burning area) | 60 s: SerpentMeat, LoxMeat, BugMeat, BoneMawSerpentMeat. 25 s: FishRaw, RawMeat, NeckTail, DeerMeat, WolfMeat, ChickenMeat, HareMeat, VoltureMeat, AsksvinMeat, BjornMeat, MooseMeat |
| piece_oven | 4 | no fire; `m_useFuel`, fuel **Wood**, maxFuel 10, secPerFuel **2000**, useFuelWhileEmpty **true** (it burns wood even when empty) | 50 s each: LoxPieUncooked→LoxPie, BreadDough→Bread, FishAndBreadUncooked→FishAndBread, MeatPlatterUncooked, HoneyGlazedChickenUncooked, MisthareSupremeUncooked, MagicallyStuffedShroomUncooked, RoastedCrustPieUncooked, PiquantPieUncooked, VikingCupcakeUncooked, SealBlubber→CookedSealBlubber, OvenPancakeUncooked, KaleChipsUncooked, BakedPoteitrUncooked |
| piece_FrostFoundry | 1 | fuel FrozenFuel | Deep North gold gear, not food. Skip it |

Overcooked item is **Coal** for all of them.

Mechanics:
- `UpdateCooking()` runs every 1 s (`InvokeRepeating`). It only advances on the **owner**: `delta = now - ZDO s_startTime`. It runs while `(m_requireFire && IsFireLit()) || (m_useFuel && GetFuel()>0 && (m_useFueldWhileEmpty || HaveUncookedItem()))`.
- Per slot:
  - `cookedTime > m_cookTime*2` → Burnt (the slot becomes "Coal", status Burnt).
  - `cookedTime > m_cookTime` → Done (the slot becomes the `m_to` name).
  - So the Cook has `m_cookTime` seconds after done to take it off: **25 s on the spit**, 60 s for iron 60-s items, 50 s in the oven.
- ZDO per slot i:
  - `"slot"+i` string: the item prefab name (input, `m_to`, or "Coal").
  - `"slot"+i` float: cookedTime. It's the same key name in the float map.
  - `"slotstatus"+i` int: 0 NotDone, 1 Done, 2 Burnt.
  - `ZDOVars.s_cheatedQueued + i` bool.
  - Station-wide: `ZDOVars.s_fuel` float and `ZDOVars.s_startTime` long.
  - Helpers (publicized): `GetSlot(int, out string, out float, out Status, out bool)`, `int GetFreeSlot()` (-1 if full), `bool IsItemDone(string)` (true for `m_to` *or* the overcooked item), `bool HaveDoneItem()`, `bool HaveUncookedItem()`, `bool IsFireLit()`, `float GetFuel()`, `bool IsItemAllowed(string)`, `ItemConversion GetItemConversion(string)`.
- **Putting an item on:** what a player does is `CookItem`, which removes one item from the inventory and then calls `m_nview.InvokeRPC("RPC_AddItem", prefabName, cheated)`. `RPC_AddItem(long, string itemName, bool cheated)` checks `IsItemAllowed` and `GetFreeSlot`, calls `SetSlot`, broadcasts `RPC_SetSlotVisual` and plays the add effect. **It does not check fire.** That check is in `OnUseItem`, so check `IsFireLit()` (spit) or `GetFuel() > 0` (oven) first. Note `CookItem` claims ownership if the station has none.
- **Taking an item off:** `m_nview.InvokeRPC("RPC_RemoveDoneItem", Vector3 userPoint, int amount)`. It finds the first slot that `IsItemDone`, which **includes burnt Coal**, spawns `amount` ItemDrops of the slot's item (at `m_spawnPoint` for the oven, else 0.5 m from the slot toward `userPoint`, with velocity), and clears the slot. `OnInteract` (player only) also rolls the cooking-skill bonus using `Player.m_localPlayer`; don't call it. Pass `amount = 1`. The spawned drops then need collecting, the same way as the smelter `RPC_EmptyProcessed` output today. `SpawnItem` calls `Game.instance.GetPlayerProfile().IncrementStatItemCraft`; on a dedicated server Game.Awake creates a dev profile, so it's non-null (inferred from Game.Awake).
- **Fuel (oven):** `InvokeRPC("RPC_AddFuel")` adds 1, **with no max check in the RPC**. Check `GetFuel() <= m_maxFuel-1` first (the switch does), and remove the Wood yourself.
- **Fire check:** `EffectArea.IsPointPlus025InsideBurningArea(checkPoint.position)` (`m_cheapFireCheck` when the radius is 0.25). A lit Fireplace (campfire or hearth) supplies the Burning EffectArea, which only exists while it has fuel. The Steward's fires chore keeps fires fuelled.

---

## 7. Crafting stations for the Cook

| Prefab | m_name | m_craftRequireFire | m_craftRequireRoof | Extensions → max level |
|---|---|---|---|---|
| **piece_cauldron** | $piece_cauldron | **true** | false | cauldron_ext1_spice, cauldron_ext3_butchertable, cauldron_ext4_pots, cauldron_ext5_mortarandpestle, cauldron_ext6_rollingpins, cauldron_ext7_smoker (maxStationDistance 5) → **level 7** (there's no ext2 in this build) |
| **piece_preptable** (food preparation table) | $piece_preptable | false | **true** | none → level 1 |
| **piece_MeadCauldron** (mead ketill; name confirmed) | $piece_meadcauldron | **true** | false | none → level 1 |

All three: `m_useDistance 1.9`, skill Cooking. The fermenter is separate (`Fermenter`, 2400 s, 6 meads per base; 3 for Bzerker).

- `int CraftingStation.GetLevel(bool checkExtensions=true)` returns `1 + GetExtentionCount()`. `GetExtensions()` refreshes `m_attachedExtensions` once 2 s have passed on `m_updateExtensionTimer`, using `StationExtension.FindExtensions(station, pos, list)`: every `StationExtension` with `Distance < ext.m_maxStationDistance`, `ext.m_craftingStation.m_name == station.m_name`, de-duplicated by extension name unless `m_stack`. Stations: `CraftingStation.m_allStations` (static list); `FindStationsInRange(name, point, range, list)`; `FindClosestStationInRange(name, point, range)`.
- **Fire:** `m_craftRequireFire` makes `Start()` run `InvokeRepeating("CheckFire",1,1)`, which sets `m_haveFire = EffectArea.IsPointPlus025InsideBurningArea(transform.position)`. The cauldron and mead ketill need a fire under them. Use `station.m_haveFire`, or call the EffectArea check directly. `CheckUsable(Player, bool)` needs a Player, so don't use it.
- **Roof** (prep table): `Cover.GetCoverForPoint(m_roofCheckPoint.position, out float cover, out bool underRoof, 0.5f)`. It needs `underRoof && cover >= 0.7`.
- **Recipes:** `ObjectDB.instance.m_recipes` (`List<Recipe>`). `Recipe : ScriptableObject` fields:
  - `ItemDrop m_item`, `int m_amount`, `bool m_enabled`
  - `CraftingStation m_craftingStation` (a prefab component; compare **`m_craftingStation.m_name`** with the station's `m_name`), `m_repairStation`
  - `int m_minStationLevel`; `int GetRequiredStationLevel(int quality)` returns `Max(1,m_minStationLevel)+(quality-1)`
  - `bool m_requireOnlyOneIngredient` (only Recipe_Fish1 among food recipes), `Piece.Requirement[] m_resources`
  - `Recipe.GetAmount(...)` touches `Player.m_localPlayer` when `m_requireOnlyOneIngredient`, so avoid it and use `m_amount`.
  - Also check `m_item.m_itemData.m_shared.m_dlc` is empty.
- **What a player's craft does** (`InventoryGui.DoCrafting(Player)`), for quality 1:
  1. `player.HaveRequirements(recipe, false, 1, multiplier)`
  2. bonus roll: `skillFactor(Cooking) * m_craftBonusChance` adds `m_craftBonusAmount`
  3. `inventory.CanAddItem(recipe.m_item.gameObject, amount)`
  4. `inventory.AddItem(recipe.m_item.gameObject.name, amount, quality 1, variant, playerID, playerName, new Vector2i(-1,-1), cheated)`
  5. `player.ConsumeResources(recipe.m_resources, 1, -1, multiplier)`: for each requirement, `RemoveItem(req.m_resItem.m_itemData.m_shared.m_name, req.GetAmount(1)*mult)` (GetAmount(1) is `m_amount`)
  6. `RaiseSkill`, then `station.m_craftItemDoneEffects.Create(...)`.

  **Doing the same without a player:** for each `req` check `container.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name) >= req.m_amount` (minus the reserves). Remove them with `RemoveItem(name, req.m_amount)`. Then call `targetInv.AddItem(recipe.m_item.gameObject.name, recipe.m_amount, 1, 0, 0L, "", Vector2i(-1,-1), false, false, **dropIfFullInv:false**)`; the default true path uses `Player.m_localPlayer` (NRE on a server). Or spawn the prefab as an ItemDrop. Stacking ignores crafterID (`FindFreeStackItem` checks name, quality and worldLevel). Optional extras: `station.PokeInUse()` and `station.m_craftItemDoneEffects.Create(pos, Quaternion.identity)`.

### Food recipes in this build (station, min level, output ×amount ← ingredients)

**Cauldron**
- L1: BoarJerky×2 (RawMeat1, Honey1); CarrotSoup (Mushroom1, Carrot3); CookedEgg (ChickenEgg1); DeerStew (CookedDeerMeat1, Blueberries1, Carrot1); MinceMeatSauce (RawMeat1, NeckTail1, Carrot1); PulledBear (CookedBjornMeat1, Carrot2, Blueberries1); QueensJam×4 (Raspberry8, Blueberries6).
- L2: BlackSoup (Bloodbag1, Honey1, Turnip1); OnionSoup (Onion3); Sausages×4 (Entrails4, RawMeat1, Thistle1); SerpentStew (Mushroom1, SerpentMeatCooked1, Honey2); ShocklateSmoothie (Ooze1, Raspberry2, Blueberries2); TurnipStew (RawMeat1, Turnip3).
- L3: Eyescream (GreydwarfEye3, FreezeGland1); WolfJerky×2 (WolfMeat1, Honey1); WolfMeatSkewer (WolfMeat1, Mushroom2, Onion1).
- L4: BloodPudding (Thistle2, Bloodbag2, BarleyFlour4); FishWraps (FishCooked2, BarleyFlour4).
- L5: FierySvinstew (AsksvinMeat1, Vineberry2, MushroomSmokePuff1); MushroomOmelette (ChickenEgg3, JotunPuffs3); Salad×3 (JotunPuffs3, Onion3, Cloudberry3); SeekerAspic×2 (BugMeat2, Magecap2, RoyalJelly2); SizzlingBerryBroth (Sap3, Fiddleheadfern2, Vineberry2); SpicyMarmalade (Vineberry3, Honey1, Fiddleheadfern1); YggdrasilPorridge (Sap4, Barley3, RoyalJelly2).
- L6: KaleChipsUncooked×4 (Kale12); Lingondricka (Lingonberry5, Ice5); MarinatedGreens (Sap3, Magecap2, Fiddleheadfern2, SmokePuff2); MashedMeat (AsksvinMeat1, VoltureMeat1, Fiddleheadfern1); OatmealLingonberryJam (Oat2, Lingonberry2, OatMilk1); OvenPancakeUncooked (MooseMeat1, Poteitr2, Lingonberry2, OatFlour2); Pancakes×3 (OatMilk1, OatFlour2, ChickenEgg2, Blueberries2); ScorchingMedley×3 (JotunPuffs3, Onion3, Fiddleheadfern3); SparklingShroomshake (Sap4, Vineberry2, SmokePuff2, Magecap2).
- L7: BakedPoteitrUncooked (SealBlubber1, Kale2, Poteitr1, OatFlour2); FishSoup (FishRaw3, Kale2, Ice2); MeatballsMashedPoteitr (MooseMeat1, Lingonberry2, Poteitr2); MooseKebab×2 (MooseMeat1, Kale2, OatFlour1, Lingonberry2); SealSoup (SealBlubber2, Kale2, Ice2); SmokedFish (FishRaw1, Kale1, Poteitr1); SmokedMooseMeat (MooseMeat1, Kale2).

**Prep table (L1)**
- BreadDough×2 (BarleyFlour10); FishAndBreadUncooked (Fish9 1, BreadDough2); HoneyGlazedChickenUncooked (ChickenMeat1, Honey3, JotunPuffs2); LoxPieUncooked (Cloudberry2, LoxMeat2, BarleyFlour4); MagicallyStuffedShroomUncooked (Magecap3, GiantBloodSack1, Turnip2); MeatPlatterUncooked (BugMeat1, LoxMeat1, HareMeat1); MisthareSupremeUncooked (HareMeat1, JotunPuffs3, Carrot2); PiquantPieUncooked (Vineberry2, AsksvinMeat2, BarleyFlour4); RoastedCrustPieUncooked (Vineberry2, VoltureEgg1, BarleyFlour4); VikingCupcakeUncooked (Cloudberry2, ChickenEgg1, BarleyFlour1, Honey1).
- Feast materials for all 9 biomes (each needs a Spice<Biome>); Recipe_Fish1 (FishRaw from any one fish, `m_requireOnlyOneIngredient`); fishing baits ×20. These are probably out of scope.

**Mead ketill (L1)**: 20 MeadBase* plus BarleyWineBase (fermenter inputs; the fermenter is out of scope), and **OatMilk** (Oat5, Ice5), which is a direct food and an ingredient of Pancakes and OatmealLingonberryJam.

**Chains:** oven inputs come from the prep table or cauldron, e.g. BreadDough→Bread, *Uncooked→oven. Some recipes need cooked outputs: DeerStew←CookedDeerMeat, PulledBear←CookedBjornMeat, SerpentStew←SerpentMeatCooked, FishWraps←FishCooked. Flours come from the windmill (a Smelter): Barley→BarleyFlour, OatSeeds→Oat, Oat→OatFlour.

---

## 8. Biome info for level-gating

There's no biome field on items or recipes. Options:
- **Cauldron level as the gate.** `m_minStationLevel` already rises with progression: L1 Meadows/BF, L2 Swamp/Ocean, L3 Mountain, L4 Plains, L5 Mistlands plus early Ashlands, L6 Ashlands plus early Deep North, L7 Deep North. It's clean for the cauldron and useless for the prep table, ketill and spits, which are all L1.
- **Highest-biome ingredient** (recommended, extending the Steward's `choreLevels` style). Keep an ingredient→biome table in the data file and take the max over a recipe's resources, recursively for cooked inputs. Suggested table **[knowledge; Deep North entries inferred from this build's data]**:
  - Meadows: RawMeat, NeckTail, DeerMeat, Honey, Raspberry, Mushroom, Dandelion, FishRaw, ChickenEgg*.
  - Black Forest: Blueberries, Carrot, Thistle, MushroomYellow, GreydwarfEye, BjornMeat.
  - Swamp: Turnip, Bloodbag, Entrails, Ooze.
  - Ocean: SerpentMeat, SerpentMeatCooked.
  - Mountain: Onion, WolfMeat, FreezeGland.
  - Plains: Barley, BarleyFlour, Cloudberry, LoxMeat, ChickenMeat, ChickenEgg*, Flax.
  - Mistlands: MushroomJotunPuffs, MushroomMagecap, Sap, BugMeat, HareMeat, RoyalJelly, GiantBloodSack.
  - Ashlands: AsksvinMeat, VoltureMeat, VoltureEgg, Vineberry, Fiddleheadfern, MushroomSmokePuff, BoneMawSerpentMeat.
  - Deep North: MooseMeat, Kale, Poteitr, Oat, OatFlour, OatMilk, Lingonberry, SealBlubber, Ice.

  (*Chickens come from Plains goblin eggs; treat ChickenEgg and ChickenMeat as Plains.)
- Crops for the Farmer, by the same table: Carrot BF, Turnip Swamp, Onion Mountain, Barley/Flax Plains, JotunPuffs/Magecap Mistlands, Kale/Oat/Poteitr Deep North.
- Food stats as a fallback tier signal: read `m_shared.m_food`, `m_foodStamina`, `m_foodEitr` from the item prefab. I dumped them all (e.g. CarrotSoup 15/45, SerpentStew 80/26, MooseKebab 110/37, OatmealLingonberryJam 39/115/85), but they don't sort cleanly by biome, so don't rely on them alone.

---

## Gotchas found (important for server-side workers)
1. `Pickable.RPC_Pick` dereferences `Player.m_localPlayer` and throws an NRE on a dedicated-server owner. Harvest manually: compute the yield, then `InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true)`.
2. `Inventory.AddItem(..., dropIfFullInv: true)` (the default) uses `Player.m_localPlayer` when the inventory is full. Pass `false`, or check `CanAddItem` first.
3. `Recipe.GetAmount` and `CookingStation.OnInteract/OnUseItem/SpawnItem(m_recordCrafter)` use `Player.m_localPlayer`. Use the RPCs and fields directly.
4. `CookingStation.RPC_AddItem` doesn't check fire, and `RPC_AddFuel` doesn't check max. Check both yourself.
5. `RPC_RemoveDoneItem` removes burnt Coal too, and spawns world ItemDrops rather than adding to an inventory.
6. Spit timing: done at 25 s, burnt at 50 s, and it only advances where the station's owner simulates it.
7. Saplings planted where they can't grow are destroyed at grow time (`m_destroyIfCantGrow`), which wastes the seed. Pre-check biome, cultivation, roof, space and cold/heat.
8. PlantEverything can change crop biome masks, costs and returns at runtime, so always read the live prefab via `ZNetScene.instance.GetPrefab(name)`.
9. PlantEasily is client-only and only partly configured on a dedicated server, so fall back to `m_growRadius*2`.

Unverified: PlantEasily's BepInDependency target (didn't decode); whether `Heightmap.m_paintMask` is filled for zones a dedicated server loads with no player nearby (vanilla Plant relies on it, so probably yes); the exact paint-mask writes by `cultivate_v2`'s TerrainOp (not decompiled).
