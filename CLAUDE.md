# Heirloom Tools: notes for Claude Code

A Stardew Valley 1.6 SMAPI mod (C#, net6.0, ModBuildConfig 4.4.0, Harmony), by ygsnaffy. Sibling of `../StardewValley-MemoryAlbum` and `../StardewValley-StardropPieces`, and follows the same conventions: `Framework/` for logic, `Integrations/` for mod APIs, all player-facing text in `i18n/default.json`, GMCM optional.

## Concept
Every tool and weapon keeps a lifetime record (stats, dated "moments", the day it joined you, an optional name), shown at the bottom of its tooltip. See README.md for the player-facing feature list.

## Layout
- `ModEntry.cs`: event wiring, rename key and the naming menu.
- `Framework/HeirloomRecord.cs`: reads/writes the record in the tool's `modData` (`ygsnaffy.HeirloomTools/...`): `Since`, `Retro`, `Name`, `Count/<stat>`, `Moments` (JSON), `KillsBy` (JSON), `BestFish`/`BestFishSize`. Parsed JSON is cached by raw string because tooltips read it every frame.
- `Framework/ToolStats.cs`: stat keys, which tools are tracked, stats shown per tool type (first one is the main stat, which gets milestones).
- `Framework/Recorder.cs`: turns events into counts, milestones and moments (rare-find and legendary fish lists live here).
- `Framework/HarmonyPatches.cs`: all patches (prefix/postfix/finalizer only).
- `Framework/TooltipRenderer.cs`: measures and draws the record; `Framework/Text.cs`: all formatting.
- `Framework/Stamper.cs`: first-load setup (stamp every tool in the world; backfill from game stats on existing saves) and stamping new tools on `InventoryChanged`.

## Implementation notes (verified against the 1.6.15 decompile)
- Most stats are measured as deltas of the player's game stats around each tool's `DoFunction` (and `MeleeWeapon.DoDamage`): `dirtHoed`, `ArtifactSpotsDug`, `rocksCrushed`, `TreesChopped`, `stumpsChopped` (resource clumps only), `weedsEliminated`, `TimesPanned`. The game increments these synchronously inside the action.
- Finds are the new `location.debris` entries after the action (or after `GameLocation.onMonsterKilled`, which is private and patched by name).
- Specific patches: `HoeDirt.performToolAction` (dry to watered), `Grass.performToolAction` (`numberOfWeeds` drop), `Tree.performTreeFall` (tree stumps; `health == -100` means the stump broke), `FishingRod.pullFishFromWater` (runs on the catching client; has `isBossFish`), `FarmAnimal.HandleStatsOnProduceCollected` (milk pail/shears, via the current-tool context), `Tool.UpgradeFrom` (Clint upgrades; `actionWhenPurchased` then `getOne()`s the tool, which copies modData).
- Tooltips: `Tool` and `MeleeWeapon` each override `getExtraSpaceNeededForTooltipSpecialIcons` and `drawTooltip` without calling base, so both are patched. The record may widen the tooltip (up to 560px) by returning a larger X. `drawHoverText` adds rod attachment slots after `drawTooltip`, so they stay below.
- Names: postfix on the `Tool.DisplayName` getter. `ToolNames.GetRealName` bypasses it.
- `NamingMenu`'s text box trims its text to its width, so after widening it the text is set again.
- Only record when the acting farmer `IsLocalPlayer`.
- The game font has no `·` or `★` glyphs; keep i18n text to plain ASCII punctuation.

## Building and testing
- `dotnet build` deploys to `Mods/HeirloomTools`; `dotnet build -c Release` also creates the zip under `bin/Release/net6.0/`.
- Tested in-game (1.6.15, SMAPI 4.5.2, single player, 1920x1080) with a throwaway harness mod that loads the save, runs real tool actions through the game code (hoe on dirt and an artifact spot, watering and refilling, breaking a stone, felling a tree and clearing its stump, scythe swing on grass, killing a slime, catching a fish and a legendary fish, Clint-style upgrade, renaming, milestones, stamping a picked-up tool) and checks the records: 29/29 passed. It also rendered the Nexus screenshots in `docs/screenshots/` by staging showcase tools and drawing the world (via Memory Album's `PhotoCamera.RenderView`), the inventory menu, and `IClickableMenu.drawHoverText` at fixed coordinates.
- Not tested in-game yet: pan, milk pail, shears, Return Scepter, slingshot kills, multiplayer, split-screen, other zoom/UI scales, a brand new farm (non-retro stamping on day 1).
- `tools/publish-nexus.ps1` uploads a new version of the Nexus main file once `tools/nexus.json` is filled in (the first upload must be done on the website).
- Decompile with `DOTNET_ROLL_FORWARD=Major ilspycmd`.
