using System;
using StardewValley;

namespace HeirloomTools.Framework;

/// <summary>Shows a tool's nickname in place of its display name.</summary>
public static class ToolNames
{
    /*********
    ** Fields
    *********/
    /// <summary>Whether nicknames are shown, from the config.</summary>
    public static Func<bool> Enabled = () => true;

    /// <summary>Set while reading a tool's real name, so the display name patch leaves it alone.</summary>
    [ThreadStatic]
    private static bool ReadingRealName;


    /*********
    ** Public methods
    *********/
    /// <summary>Get the tool's name as the game would show it without a nickname.</summary>
    public static string GetRealName(Tool tool)
    {
        ReadingRealName = true;
        try
        {
            return tool.DisplayName;
        }
        finally
        {
            ReadingRealName = false;
        }
    }

    /// <summary>Get the nickname to show in place of the display name, if any.</summary>
    public static string? GetNicknameToShow(Tool tool)
    {
        if (ReadingRealName || !Enabled())
            return null;
        return HeirloomRecord.GetName(tool);
    }
}
