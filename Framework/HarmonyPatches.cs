using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace HeirloomTools.Framework;

/// <summary>The Harmony patches that watch tool use and add the record to tooltips.</summary>
/// <remarks>
/// Most tool actions are measured by snapshotting the player's game stats (which the game updates inside each tool's
/// <c>DoFunction</c>) before and after the action, and by looking at the debris the action dropped. That keeps the
/// patches to prefixes and postfixes, so they don't conflict with other mods.
/// </remarks>
public static class HarmonyPatches
{
    /*********
    ** Fields
    *********/
    private static IMonitor Monitor = null!;
    private static Recorder Recorder = null!;
    private static TooltipRenderer Tooltip = null!;

    /// <summary>The tool whose action is running right now, if any.</summary>
    [ThreadStatic]
    private static Tool? CurrentTool;

    /// <summary>What was true before a tool action, to compare against afterwards.</summary>
    public class ActionState
    {
        public Tool Tool = null!;
        public Farmer Who = null!;
        public GameLocation Location = null!;
        public Dictionary<string, uint> Stats = new();
        public HashSet<Debris> Debris = new(ReferenceEqualityComparer.Instance);
        public int WaterLeft;
    }

    /// <summary>The game stats to watch, and the tool stat each one feeds.</summary>
    private static readonly (string GameStat, string ToolStat, Func<Tool, bool> Applies)[] WatchedStats =
    {
        ("dirtHoed", ToolStats.Tilled, t => t is Hoe),
        ("ArtifactSpotsDug", ToolStats.ArtifactSpots, t => t is Hoe),
        ("rocksCrushed", ToolStats.Rocks, t => t is Pickaxe),
        ("TreesChopped", ToolStats.Trees, t => t is Axe),
        ("stumpsChopped", ToolStats.Stumps, t => t is Axe),
        ("weedsEliminated", ToolStats.Weeds, t => t is MeleeWeapon),
        ("TimesPanned", ToolStats.Pans, t => t is Pan)
    };


    /*********
    ** Public methods
    *********/
    public static void Apply(Harmony harmony, IMonitor monitor, Recorder recorder, TooltipRenderer tooltip)
    {
        Monitor = monitor;
        Recorder = recorder;
        Tooltip = tooltip;

        // tool actions
        foreach (Type type in new[] { typeof(Hoe), typeof(Pickaxe), typeof(Axe), typeof(WateringCan), typeof(Pan), typeof(MilkPail), typeof(Shears), typeof(Wand) })
        {
            Patch(harmony, AccessTools.DeclaredMethod(type, nameof(Tool.DoFunction)), prefix: nameof(Before_DoFunction), postfix: nameof(After_DoFunction), finalizer: nameof(Finally_ClearTool));
        }
        Patch(harmony, AccessTools.DeclaredMethod(typeof(MeleeWeapon), nameof(MeleeWeapon.DoDamage)), prefix: nameof(Before_DoDamage), postfix: nameof(After_DoFunction), finalizer: nameof(Finally_ClearTool));

        // specific results
        Patch(harmony, AccessTools.DeclaredMethod(typeof(HoeDirt), nameof(HoeDirt.performToolAction)), prefix: nameof(Before_HoeDirtToolAction), postfix: nameof(After_HoeDirtToolAction));
        Patch(harmony, AccessTools.DeclaredMethod(typeof(Grass), nameof(Grass.performToolAction)), prefix: nameof(Before_GrassToolAction), postfix: nameof(After_GrassToolAction));
        Patch(harmony, AccessTools.DeclaredMethod(typeof(Tree), "performTreeFall"), prefix: nameof(Before_TreeFall), postfix: nameof(After_TreeFall));
        Patch(harmony, AccessTools.DeclaredMethod(typeof(GameLocation), "onMonsterKilled"), prefix: nameof(Before_MonsterKilled), postfix: nameof(After_MonsterKilled));
        Patch(harmony, AccessTools.DeclaredMethod(typeof(FishingRod), nameof(FishingRod.pullFishFromWater)), postfix: nameof(After_PullFishFromWater));
        Patch(harmony, AccessTools.DeclaredMethod(typeof(FarmAnimal), nameof(FarmAnimal.HandleStatsOnProduceCollected)), postfix: nameof(After_ProduceCollected));
        Patch(harmony, AccessTools.DeclaredMethod(typeof(Tool), nameof(Tool.UpgradeFrom)), postfix: nameof(After_UpgradeFrom));

        // names and tooltips
        Patch(harmony, AccessTools.DeclaredPropertyGetter(typeof(Tool), nameof(Tool.DisplayName)), postfix: nameof(After_DisplayName));
        foreach (Type type in new[] { typeof(Tool), typeof(MeleeWeapon) })
        {
            Patch(harmony, AccessTools.DeclaredMethod(type, nameof(Item.getExtraSpaceNeededForTooltipSpecialIcons)), postfix: nameof(After_GetExtraTooltipSpace));
            Patch(harmony, AccessTools.DeclaredMethod(type, nameof(Item.drawTooltip)), postfix: nameof(After_DrawTooltip));
        }
    }


    /*********
    ** Private methods
    *********/
    private static void Patch(Harmony harmony, System.Reflection.MethodBase? method, string? prefix = null, string? postfix = null, string? finalizer = null)
    {
        if (method is null)
        {
            Monitor.Log($"Couldn't find a method to patch (prefix {prefix}, postfix {postfix}). Some stats won't be recorded.", LogLevel.Warn);
            return;
        }

        harmony.Patch(
            method,
            prefix: prefix != null ? new HarmonyMethod(typeof(HarmonyPatches), prefix) : null,
            postfix: postfix != null ? new HarmonyMethod(typeof(HarmonyPatches), postfix) : null,
            finalizer: finalizer != null ? new HarmonyMethod(typeof(HarmonyPatches), finalizer) : null
        );
    }

    /// <summary>Run an action, logging instead of crashing if it fails. The patches must never break the game's own logic.</summary>
    private static void Safely(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Monitor.LogOnce($"Failed in {name}: {ex}", LogLevel.Error);
        }
    }

    private static ActionState? Snapshot(Tool tool, GameLocation location, Farmer who)
    {
        if (who is null || !who.IsLocalPlayer || location is null)
            return null;

        ActionState state = new() { Tool = tool, Who = who, Location = location };
        foreach (var watched in WatchedStats)
        {
            if (watched.Applies(tool))
                state.Stats[watched.GameStat] = who.stats.Get(watched.GameStat);
        }
        foreach (Debris debris in location.debris)
            state.Debris.Add(debris);
        if (tool is WateringCan can)
            state.WaterLeft = can.WaterLeft;
        return state;
    }

    private static IEnumerable<string> GetNewDebrisItems(GameLocation location, HashSet<Debris> before)
    {
        List<string> items = new();
        foreach (Debris debris in location.debris)
        {
            if (before.Contains(debris))
                continue;

            string? id = debris.item?.QualifiedItemId ?? (string.IsNullOrEmpty(debris.itemId.Value) ? null : ItemRegistry.QualifyItemId(debris.itemId.Value));
            if (id != null)
                items.Add(id);
        }
        return items;
    }

    /****
    ** Tool actions
    ****/
    private static void Before_DoFunction(Tool __instance, GameLocation location, Farmer who, out ActionState? __state)
    {
        ActionState? state = null;
        Safely(nameof(Before_DoFunction), () =>
        {
            state = Snapshot(__instance, location, who);
            if (state != null)
                CurrentTool = __instance;
        });
        __state = state;
    }

    private static void Before_DoDamage(MeleeWeapon __instance, GameLocation location, Farmer who, out ActionState? __state)
    {
        Before_DoFunction(__instance, location, who, out __state);
    }

    private static void After_DoFunction(Tool __instance, ActionState? __state)
    {
        if (__state is null)
            return;

        Safely(nameof(After_DoFunction), () =>
        {
            foreach (var watched in WatchedStats)
            {
                if (__state.Stats.TryGetValue(watched.GameStat, out uint before))
                {
                    uint after = __state.Who.stats.Get(watched.GameStat);
                    if (after > before)
                        Recorder.AddStat(__instance, watched.ToolStat, after - before);
                }
            }

            switch (__instance)
            {
                case WateringCan can when can.WaterLeft > __state.WaterLeft:
                    Recorder.AddStat(can, ToolStats.Refills);
                    break;

                case Wand when !__state.Who.bathingClothes.Value && !__state.Who.onBridge.Value:
                    Recorder.AddStat(__instance, ToolStats.Warps);
                    break;
            }

            if (__instance is Hoe or Pickaxe or Axe or Pan)
                Recorder.RecordFinds(__instance, GetNewDebrisItems(__state.Location, __state.Debris));
        });
    }

    private static Exception? Finally_ClearTool(Exception? __exception)
    {
        CurrentTool = null;
        return __exception;
    }

    /****
    ** Specific results
    ****/
    private static void Before_HoeDirtToolAction(HoeDirt __instance, Tool t, out int __state)
    {
        __state = __instance.state.Value;
    }

    private static void After_HoeDirtToolAction(HoeDirt __instance, Tool t, int __state)
    {
        if (t is WateringCan && __state != HoeDirt.watered && __instance.state.Value == HoeDirt.watered && t.getLastFarmerToUse()?.IsLocalPlayer == true)
            Safely(nameof(After_HoeDirtToolAction), () => Recorder.AddStat(t, ToolStats.Watered));
    }

    private static void Before_GrassToolAction(Grass __instance, out int __state)
    {
        __state = __instance.numberOfWeeds.Value;
    }

    private static void After_GrassToolAction(Grass __instance, Tool t, int __state)
    {
        if (t is MeleeWeapon && t.getLastFarmerToUse()?.IsLocalPlayer == true)
        {
            int cut = __state - Math.Max(0, __instance.numberOfWeeds.Value);
            if (cut > 0)
                Safely(nameof(After_GrassToolAction), () => Recorder.AddStat(t, ToolStats.Grass, cut));
        }
    }

    private static void Before_TreeFall(Tree __instance, out bool __state)
    {
        // whether this hit will clear a stump rather than fell a tree
        __state = __instance.stump.Value && __instance.health.Value != -100f;
    }

    private static void After_TreeFall(Tree __instance, Tool t, bool __state)
    {
        if (__state && t is Axe && __instance.health.Value == -100f && t.getLastFarmerToUse()?.IsLocalPlayer == true)
            Safely(nameof(After_TreeFall), () => Recorder.AddStat(t, ToolStats.Stumps));
    }

    private static void Before_MonsterKilled(GameLocation __instance, out HashSet<Debris> __state)
    {
        __state = new HashSet<Debris>(__instance.debris, ReferenceEqualityComparer.Instance);
    }

    private static void After_MonsterKilled(GameLocation __instance, Farmer who, Monster monster, bool killedByBomb, HashSet<Debris> __state)
    {
        if (who is null || !who.IsLocalPlayer || killedByBomb)
            return;

        Safely(nameof(After_MonsterKilled), () =>
        {
            Tool? weapon = CurrentTool as MeleeWeapon ?? who.CurrentTool as MeleeWeapon ?? (Tool?)(who.CurrentTool as Slingshot);
            if (weapon is null)
                return;

            Recorder.RecordKill(weapon, monster.Name);
            Recorder.RecordFinds(weapon, GetNewDebrisItems(__instance, __state));
        });
    }

    private static void After_PullFishFromWater(FishingRod __instance, string fishId, int fishSize, bool treasureCaught, bool wasPerfect, bool isBossFish, int numCaught)
    {
        if (__instance.getLastFarmerToUse()?.IsLocalPlayer != true)
            return;

        Safely(nameof(After_PullFishFromWater), () => Recorder.RecordCatch(__instance, fishId, fishSize, wasPerfect, treasureCaught, isBossFish, numCaught));
    }

    private static void After_ProduceCollected(uint amount)
    {
        Tool? tool = CurrentTool;
        if (tool is MilkPail)
            Safely(nameof(After_ProduceCollected), () => Recorder.AddStat(tool, ToolStats.Milked));
        else if (tool is Shears)
            Safely(nameof(After_ProduceCollected), () => Recorder.AddStat(tool, ToolStats.Sheared));
    }

    private static void After_UpgradeFrom(Tool __instance, Tool other)
    {
        Safely(nameof(After_UpgradeFrom), () => Recorder.RecordUpgrade(other, __instance));
    }

    /****
    ** Names and tooltips
    ****/
    private static void After_DisplayName(Tool __instance, ref string __result)
    {
        string? nickname = ToolNames.GetNicknameToShow(__instance);
        if (nickname != null)
            __result = nickname;
    }

    private static void After_GetExtraTooltipSpace(Item __instance, SpriteFont font, int minWidth, int startingHeight, ref Point __result)
    {
        if (__instance is not Tool tool)
            return;

        try
        {
            int width = __result.X != 0 ? __result.X : minWidth;
            if (Tooltip.Measure(tool, font, width) is not { } size)
                return;

            __result.X = size.Width;
            __result.Y = (__result.Y != 0 ? __result.Y : startingHeight) + size.Height;
        }
        catch (Exception ex)
        {
            Monitor.LogOnce($"Failed measuring a tooltip: {ex}", LogLevel.Error);
        }
    }

    private static void After_DrawTooltip(Item __instance, SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font, float alpha)
    {
        if (__instance is not Tool tool)
            return;

        try
        {
            Tooltip.Draw(spriteBatch, tool, font, x, ref y, alpha);
        }
        catch (Exception ex)
        {
            Monitor.LogOnce($"Failed drawing a tooltip: {ex}", LogLevel.Error);
        }
    }
}
