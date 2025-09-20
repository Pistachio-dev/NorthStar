using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Style;

namespace NorthStar.Helpers;

internal static class ImGuiHelper
{
    private static bool InternalIconButton(Func<ImU8String, bool> func, FontAwesomeIcon icon, string? id = null)
    {
        var label = icon.ToIconString();
        if (id != null)
        {
            label += $"##{id}";
        }

        ImGui.PushFont(UiBuilder.IconFont);
        var ret = func(label);
        ImGui.PopFont();

        return ret;
    }

    internal static bool SmallIconButton(FontAwesomeIcon icon, string? id = null)
    {
        return InternalIconButton(ImGui.SmallButton, icon, id);
    }

    internal static bool IconButton(FontAwesomeIcon icon, string? id = null)
    {
        return InternalIconButton(id => ImGui.Button(id), icon, id);
    }

    internal static void HelpIcon(string text)
    {
        var colour = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        ImGui.PushStyleColor(ImGuiCol.Text, colour);
        ImGui.TextUnformatted("(?)");
        ImGui.PopStyleColor();

        TextTooltip(text);
    }

    internal static unsafe ImGuiListClipperPtr Clipper(int itemsCount)
    {
        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
        clipper.Begin(itemsCount);

        return clipper;
    }

    internal static void TextTooltip(string tooltip)
    {
        if (!ImGui.IsItemHovered())
        {
            return;
        }

        var width = ImGui.CalcTextSize("m") * 40;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(width.X);
        ImGui.TextUnformatted(tooltip);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    internal static void WarningText(string text)
    {
        var style = StyleModel.GetConfiguredStyle() ?? StyleModel.GetFromCurrent();
        var dalamudOrange = style.BuiltInColors?.DalamudOrange;
        if (dalamudOrange != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, dalamudOrange.Value);
        }

        ImGui.PushTextWrapPos();
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();

        if (dalamudOrange != null)
        {
            ImGui.PopStyleColor();
        }
    }
}