using System;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace HeirloomTools.Framework;

/// <summary>SMAPI console commands for viewing and editing tool records.</summary>
public class ConsoleCommands
{
    /*********
    ** Fields
    *********/
    private readonly IMonitor Monitor;
    private readonly Text Text;
    private readonly Recorder Recorder;


    /*********
    ** Public methods
    *********/
    public ConsoleCommands(IMonitor monitor, Text text, Recorder recorder)
    {
        this.Monitor = monitor;
        this.Text = text;
        this.Recorder = recorder;
    }

    public void Register(ICommandHelper commands)
    {
        commands.Add("heirloom_show", "Show the record for every tool in your inventory.\n\nUsage: heirloom_show", this.Show);
        commands.Add("heirloom_name", "Name the tool you're holding, or clear its name.\n\nUsage: heirloom_name [name]", this.Name);
        commands.Add("heirloom_reset", "Erase the record of the tool you're holding and start it fresh from today.\n\nUsage: heirloom_reset", this.Reset);
        commands.Add("heirloom_add", "Add to a stat on the tool you're holding (for testing).\n\nUsage: heirloom_add <stat> <amount>\nStats: " + string.Join(", ", typeof(ToolStats).GetFields().Where(f => f.IsLiteral).Select(f => f.GetValue(null))), this.Add);
        commands.Add("heirloom_find", "Record a find on the tool you're holding (for testing).\n\nUsage: heirloom_find <qualified item ID>", this.Find);
    }


    /*********
    ** Private methods
    *********/
    private bool TryGetWorld()
    {
        if (Context.IsWorldReady)
            return true;
        this.Monitor.Log("Load a save first.", LogLevel.Error);
        return false;
    }

    private Tool? GetHeldTool()
    {
        if (Game1.player.CurrentTool is Tool tool && ToolStats.IsTracked(tool))
            return tool;
        this.Monitor.Log("Hold a tool or weapon first.", LogLevel.Error);
        return null;
    }

    private void Show(string command, string[] args)
    {
        if (!this.TryGetWorld())
            return;

        foreach (Tool tool in Game1.player.Items.OfType<Tool>().Where(ToolStats.IsTracked))
        {
            if (!HeirloomRecord.IsStarted(tool))
            {
                this.Monitor.Log($"{tool.DisplayName}: no record yet.", LogLevel.Info);
                continue;
            }

            string lines = string.Join("", this.Text.StatLines(tool).Concat(HeirloomRecord.GetMoments(tool).Select(m => this.Text.Moment(tool, m))).Select(line => $"\n    {line}"));
            this.Monitor.Log($"{tool.DisplayName}:{lines}\n    {this.Text.Since(tool)}", LogLevel.Info);
        }
    }

    private void Name(string command, string[] args)
    {
        if (!this.TryGetWorld() || this.GetHeldTool() is not { } tool)
            return;

        string name = string.Join(" ", args);
        this.Recorder.Rename(tool, name);
        this.Monitor.Log(name.Length > 0 ? $"Named it '{name}'." : "Cleared its name.", LogLevel.Info);
    }

    private void Reset(string command, string[] args)
    {
        if (!this.TryGetWorld() || this.GetHeldTool() is not { } tool)
            return;

        HeirloomRecord.Clear(tool);
        HeirloomRecord.Start(tool, HeirloomRecord.Today(), retro: false);
        this.Monitor.Log($"Started a fresh record for {tool.DisplayName}.", LogLevel.Info);
    }

    private void Add(string command, string[] args)
    {
        if (!this.TryGetWorld() || this.GetHeldTool() is not { } tool)
            return;

        if (args.Length < 2 || !long.TryParse(args[1], out long amount))
        {
            this.Monitor.Log("Usage: heirloom_add <stat> <amount>", LogLevel.Error);
            return;
        }

        this.Recorder.AddStat(tool, args[0], amount);
        this.Monitor.Log($"{args[0]} is now {HeirloomRecord.GetCount(tool, args[0])}.", LogLevel.Info);
    }

    private void Find(string command, string[] args)
    {
        if (!this.TryGetWorld() || this.GetHeldTool() is not { } tool)
            return;

        if (args.Length < 1)
        {
            this.Monitor.Log("Usage: heirloom_find <qualified item ID>", LogLevel.Error);
            return;
        }

        this.Recorder.RecordFinds(tool, new[] { ItemRegistry.QualifyItemId(args[0]) ?? args[0] });
        this.Monitor.Log("Done.", LogLevel.Info);
    }
}
