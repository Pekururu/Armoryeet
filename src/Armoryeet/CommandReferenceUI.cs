using Dalamud.Bindings.ImGui;

namespace Armoryeet;

public static class CommandReferenceUI
{
    private static readonly (string Command, string Description, string Aliases)[] General =
    [
        ("/armoryeet", "Open or close the main window.", "/yeet"),
        ("/yeet scan", "Scan enabled containers and open reviewed results.", ""),
        ("/yeet status", "Report current movement state and counts in chat.", ""),
        ("/yeet stop", "Stop the active move queue.", ""),
        ("/yeet settings", "Open or close Settings.", "/yeet config, /yeetsettings"),
        ("/yeet help", "Print a compact command summary in chat.", ""),
    ];

    private static readonly (string Command, string Description, string Aliases)[] Immediate =
    [
        ("/yeet chest", "Move results from every enabled container.", ""),
        ("/yeet weapons", "Move main-hand and off-hand results.", ""),
        ("/yeet mainhand", "Move main-hand results.", "/yeet main, mhand, mh"),
        ("/yeet offhand", "Move off-hand results.", "/yeet off, ohand, oh"),
        ("/yeet armor", "Move all armor results.", ""),
        ("/yeet head", "Move head results.", ""),
        ("/yeet body", "Move body results.", ""),
        ("/yeet hands", "Move hand results.", "/yeet gloves"),
        ("/yeet waist", "Move waist results.", "/yeet belt"),
        ("/yeet legs", "Move leg results.", ""),
        ("/yeet feet", "Move feet results.", "/yeet boots"),
        ("/yeet accessories", "Move all accessory results.", "/yeet accessory"),
        ("/yeet ears", "Move earring results.", "/yeet ear, earrings"),
        ("/yeet neck", "Move necklace results.", "/yeet necklace"),
        ("/yeet wrist", "Move wrist results.", "/yeet wrists, bracelet, bracelets"),
        ("/yeet rings", "Move ring results.", "/yeet ring"),
    ];

    public static void Draw()
    {
        ImGui.TextWrapped("Use /yeet scan for the reviewed workflow with selection and capacity checks.");
        ImGui.Spacing();
        DrawSection("Window and safety commands", General);
        ImGui.Spacing();
        ImGui.TextColored(new System.Numerics.Vector4(1, .65f, .2f, 1), "Immediate expert commands");
        ImGui.TextWrapped("The commands below scan and enqueue immediately without the reviewed confirmation window. Gearset protection and movement blockers still apply.");
        DrawSection("Immediate movement", Immediate);
        ImGui.TextDisabled("Click Copy beside any row, then paste the command into chat.");
    }

    public static void DrawCompactTooltip()
    {
        ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(410, 0), new System.Numerics.Vector2(410, float.MaxValue));
        ImGui.BeginTooltip();
        ImGui.TextUnformatted("Armoryeet commands");
        ImGui.Separator();
        ImGui.TextUnformatted("/yeet scan"); ImGui.SameLine(145); ImGui.TextDisabled("Open reviewed results");
        ImGui.TextUnformatted("/yeet status"); ImGui.SameLine(145); ImGui.TextDisabled("Show movement status");
        ImGui.TextUnformatted("/yeet stop"); ImGui.SameLine(145); ImGui.TextDisabled("Stop the active queue");
        ImGui.TextUnformatted("/yeet settings"); ImGui.SameLine(145); ImGui.TextDisabled("Open settings");
        ImGui.Spacing();
        ImGui.TextColored(new System.Numerics.Vector4(1, .65f, .2f, 1), "Immediate movement");
        ImGui.TextWrapped("/yeet chest, weapons, armor, accessories");
        ImGui.TextWrapped("/yeet mainhand, offhand, head, body, hands, waist, legs, feet, ears, neck, wrist, rings");
        ImGui.Spacing();
        ImGui.TextDisabled("Use /yeet help for aliases and the full list.");
        ImGui.EndTooltip();
    }

    private static void DrawSection(string id, (string Command, string Description, string Aliases)[] rows)
    {
        if (!ImGui.BeginTable($"commands-{id}", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings)) return;
        ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Purpose", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Copy", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableHeadersRow();
        foreach (var row in rows)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Command);
            if (!string.IsNullOrEmpty(row.Aliases)) { ImGui.TextDisabled(row.Aliases); }
            ImGui.TableNextColumn(); ImGui.TextWrapped(row.Description);
            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"Copy##{row.Command}")) ImGui.SetClipboardText(row.Command);
        }
        ImGui.EndTable();
    }
}
