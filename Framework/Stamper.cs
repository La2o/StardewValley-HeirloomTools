using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace HeirloomTools.Framework;

/// <summary>Starts the record for tools as they join the player, and sets up existing saves.</summary>
public class Stamper
{
    /*********
    ** Fields
    *********/
    private const string InitializedKey = HeirloomRecord.Prefix + "Initialized";

    private readonly IMonitor Monitor;
    private readonly Func<ModConfig> Config;

    /// <summary>The game stats that fill in each tool stat on an existing save.</summary>
    private static readonly (Func<Tool, bool> Applies, string ToolStat, string GameStat)[] Backfills =
    {
        (t => t is Hoe, ToolStats.Tilled, "dirtHoed"),
        (t => t is Hoe, ToolStats.ArtifactSpots, "ArtifactSpotsDug"),
        (t => t is Pickaxe, ToolStats.Rocks, "rocksCrushed"),
        (t => t is Axe, ToolStats.Trees, "TreesChopped"),
        (t => t is Axe, ToolStats.Stumps, "stumpsChopped"),
        (t => t is FishingRod, ToolStats.Fish, "fishCaught"),
        (t => t is Pan, ToolStats.Pans, "TimesPanned")
    };


    /*********
    ** Public methods
    *********/
    public Stamper(IMonitor monitor, Func<ModConfig> config)
    {
        this.Monitor = monitor;
        this.Config = config;
    }

    /// <summary>The first time the mod sees this player, start a record for every tool they have.</summary>
    public void InitializeIfNeeded(Farmer player)
    {
        if (player.modData.ContainsKey(InitializedKey))
        {
            this.StampInventory(player);
            return;
        }

        int today = HeirloomRecord.Today();
        bool newFarm = Game1.stats.DaysPlayed <= 1;
        List<Tool> stamped = new();

        // every tool in the world, so tools in chests aren't later mistaken for new ones
        Utility.ForEachItem(item =>
        {
            if (item is Tool tool && ToolStats.IsTracked(tool) && HeirloomRecord.Start(tool, today, retro: !newFarm))
                stamped.Add(tool);
            return true;
        });

        // and the player's own inventory, in case the world scan didn't reach it (e.g. a farmhand)
        foreach (Tool tool in player.Items.OfType<Tool>())
        {
            if (ToolStats.IsTracked(tool) && HeirloomRecord.Start(tool, today, retro: !newFarm))
                stamped.Add(tool);
        }

        if (!newFarm && this.Config().BackfillFromGameStats)
            this.Backfill(player, stamped);

        player.modData[InitializedKey] = today.ToString();
        this.Monitor.Log($"Started records for {stamped.Count} tools ({(newFarm ? "new farm" : "existing save")}).", LogLevel.Debug);
    }

    /// <summary>Start a record for any tool in the player's inventory that doesn't have one.</summary>
    public void StampInventory(Farmer player)
    {
        foreach (Item? item in player.Items)
            this.StampNew(item);
    }

    /// <summary>Start a record for a tool that just joined the player.</summary>
    public void StampNew(Item? item)
    {
        if (item is Tool tool && ToolStats.IsTracked(tool))
            HeirloomRecord.Start(tool, HeirloomRecord.Today(), retro: false);
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Fill in the player's carried tools from the farm's lifetime stats. Each stat goes to the best tool of its kind in the inventory, which is almost always the one that did the work.</summary>
    private void Backfill(Farmer player, List<Tool> stamped)
    {
        List<Tool> carried = player.Items.OfType<Tool>().Where(stamped.Contains).ToList();

        foreach (var backfill in Backfills)
        {
            Tool? best = carried
                .Where(backfill.Applies)
                .OrderByDescending(tool => tool.UpgradeLevel)
                .FirstOrDefault();
            uint value = player.stats.Get(backfill.GameStat);
            if (best is null || value == 0 || HeirloomRecord.GetCount(best, backfill.ToolStat) > 0)
                continue;

            HeirloomRecord.AddCount(best, backfill.ToolStat, value);
            this.Monitor.Log($"Backfilled {best.Name} {backfill.ToolStat} = {value}.");
        }
    }
}
