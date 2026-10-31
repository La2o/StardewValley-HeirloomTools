using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace HeirloomTools;

/// <summary>The mod settings.</summary>
public class ModConfig
{
    /// <summary>Add each tool's record to its tooltip.</summary>
    public bool ShowInTooltip { get; set; } = true;

    /// <summary>How many recent moments the tooltip shows. Hold <see cref="ExpandKey"/> to see them all.</summary>
    public int MomentsShown { get; set; } = 3;

    /// <summary>Hold this while hovering a tool to see all of its moments.</summary>
    public KeybindList ExpandKey { get; set; } = KeybindList.Parse("LeftShift");

    /// <summary>Press this while holding a tool to name it.</summary>
    public KeybindList RenameKey { get; set; } = KeybindList.Parse("N");

    /// <summary>Show a tool's name in place of its usual name.</summary>
    public bool ShowNicknames { get; set; } = true;

    /// <summary>Show a message when a tool reaches a milestone.</summary>
    public bool MilestoneMessages { get; set; } = true;

    /// <summary>When the mod is added to an existing save, fill in your current tools' totals from the farm's stats.</summary>
    public bool BackfillFromGameStats { get; set; } = true;
}
