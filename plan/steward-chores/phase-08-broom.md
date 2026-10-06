# Phase 08 — The Steward's broom

**Depends on:** nothing in this plan (it's cosmetic and independent; ordered last so the chores never wait on it) · **Enables:** 09

## Goal
The Steward's weapon becomes a broom: a new item `VFH_Broom` with a club's stats (the Steward already wields a `Club` at every level), wearing the game's own broom model. Valheim has no broom item, and "broom" appears as a name in several of its asset bundles, which may be a broom mesh. The item looks the model up by name when the vanilla prefabs are ready. If it can't find it, or the result is unusable, it uses the vanilla cultivator's model instead. The Steward sweeps with it and fights with it.

## Files touched
- `src/VikingsForHire/Hirelings/BroomItem.cs` (new):
  - registration on `PrefabManager.OnVanillaPrefabsAvailable`, created once (the `HiringCharter` guard, fixed in 0.3.0);
  - a `CustomItem("VFH_Broom", "Club", ItemConfig { Name = "$vfh_broom", Description = "$vfh_broom_desc" })`, not craftable (no recipe), with the visual swap (steps).
- `src/VikingsForHire/Plugin.cs`: `Hirelings.BroomItem.Register()`.
- `src/VikingsForHire/Core/Data/DefaultData.cs`: the Steward's `Gear` becomes `Mains("VFH_Broom" × 8)`.
- `src/VikingsForHire/Localization/English.json`: `vfh_broom` ("Broom"), `vfh_broom_desc` ("The Steward's trusty broom. Sweeps floors and the occasional Greyling.").
- `src/VikingsForHire/Commands/DebugCommands.cs`: `vfh_broom_info` (no cheat) logs which model the broom uses and its scale, for tuning.
- `docs/test-checklist.md`: row VFH-BROOM-1 (by hand).

## Steps
1. **Finding the model**, in order, stopping at the first hit:
   1. Every prefab in `ZNetScene.instance.m_prefabs`: their child `MeshFilter`s whose `sharedMesh.name` contains `broom` (case-insensitive), with the `MeshRenderer`'s shared materials.
   2. `Resources.FindObjectsOfTypeAll<Mesh>()` named like `broom`, paired with the material of the renderer that uses it, also found via `Resources.FindObjectsOfTypeAll<MeshFilter>()`.
   3. Otherwise the fallback: the `Cultivator` item prefab's visual mesh and materials.

   **A found mesh is only used if it's usable:** its longest bounds axis must be 0.8–3 m, with at least one material, under 5,000 vertices (a whole building that happens to contain "broom" is rejected). Otherwise the next source is tried, and in the end the cultivator. The choice is automatic and logged; nobody has to decide it at test time.

   Log Info `broom.model` with the source (`prefab:<name>`, `loaded`, or `fallback:Cultivator`) and the mesh's bounds.
2. **Swapping the visual:**
   - In the cloned club's prefab, find the visual child: the `MeshFilter` under the `attach` transform that the club uses in hand. Replace its `sharedMesh` and the renderer's `sharedMaterials`.
   - Scale it so the mesh's longest axis is 1.4 m.
   - Rotate it so that axis lines up with the club's own longest axis, with the bristles at the far end. If the mesh's centre of mass is nearer one end, that end is the bristles.
   - The dropped-item visual (the prefab's root `MeshFilter`, if separate) gets the same swap.
   - The icon stays the club's: there's no rendered broom icon, and players never hold this item.
3. **Gear:**
   - New data files get `VFH_Broom` for the Steward.
   - Existing files keep `Club`, because `FillMissing` never changes values. The 0.4.0 release notes tell server owners they can change it.
   - `DataValidator.Sanitize` already falls back to the level below for unknown items, so a server without the broom registered (impossible with the mod loaded) would still be fine.
4. **Hand check:** Tim looks at it in game (test plan). If the automatic choice picked a bad mesh anyway, add its name to a short `Rejected` list in `BroomItem` and record it in this file's "As built".

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes (`LocalizationCoverageTests` covers the two new strings).
- In game, `vfh_broom_info` logs a `broom.model` line with no error.

## Test plan
- **By hand (VFH-BROOM-1):** hire a Steward, with a new data file or with `Gear` edited to `VFH_Broom`. It holds a broom-looking item the right way up, swings it at a Greyling, and still does its chores (`EnsureArmed` finds its main weapon). The log shows which model was used.
- The `vfh_t_chore_*` macros still pass with the broom as the Steward's weapon.

## Commit
`feat(steward): the Steward's broom (the game's broom model on a club, cultivator as fallback)`

## Rollback
Revert the commit. New data files go back to `Club`; an existing file with `VFH_Broom` falls back to the level-below rule and then to nothing. Edit it back to `Club` after reverting.

## As built
- **`vfh_broom_info`** is registered by `BroomItem` itself, not in `DebugCommands`. The model choice is logged as `broom.model`, with `source` set to `prefab:<name>`, `loaded`, `fallback:Cultivator`, or `club` when nothing usable was found.
- **Mesh lookup:** done once, when the vanilla prefabs are ready, over every scene prefab's meshes and then the meshes already loaded.
- **Alignment:** the broom's longest axis is lined up with the club's and scaled to 1.4 m. The bristles may come out at either end; check in game, and if they're wrong, add a turn in `Swap`.
- **Timing:** the item is created on `OnVanillaPrefabsAvailable`, which also fires at the main menu. The model is dressed on `OnPrefabsRegistered`, once the scene's prefabs exist; searching earlier found nothing.
- **Built broom (replaces the lookup):** no broom mesh is loaded in the game, and the cultivator is kept for a future Farmer. The broom is now built from simple shapes when the vanilla prefabs appear: a wooden handle and binding (the `wood_pole` material) and a flat, flared bundle of bristles (the thatch roof's straw), pointed the way the club points from the hand, 1.75 m overall. `vfh_broom_info` names the materials.
- **Data check:** the data file is checked before the mod's own items are registered, which dropped `VFH_Broom` as unknown (Stewards fought bare-handed). The mod's items and Jotunn items now count as known.
