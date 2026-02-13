using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Colors;

namespace AetherBlackbox.Windows;

public class AboutWindow : Window, IDisposable
{
    public AboutWindow() : base("About Aether Blackbox")
    {
        this.Size = new Vector2(380, 320);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        ImGui.Text($"Version: {version}");
        ImGui.Separator();

        ImGui.Text("Created by: rail");
        ImGui.Spacing();

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Credits & Acknowledgements:");
        ImGui.BulletText("DeathRecap by Kouzukii");
        ImGui.Indent();
        ImGui.TextDisabled("(Core logic, event capturing, and data structures)");
        ImGui.Unindent();

        ImGui.BulletText("AetherDraw by rail");
        ImGui.Indent();
        ImGui.TextDisabled("(Canvas engine, drawing tools, and rendering)");
        ImGui.Unindent();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        float btnWidthFull = ImGui.GetContentRegionAvail().X;

        float bugReportButtonHeight = ImGui.CalcTextSize("Bug report/\nFeature request").Y + ImGui.GetStyle().FramePadding.Y * 2.0f;

        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.1f, 0.4f, 0.1f, 1.0f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.1f, 0.5f, 0.1f, 1.0f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.6f, 0.2f, 1.0f)))
        {
            if (ImGui.Button("Bug report/\nFeature request", new Vector2(btnWidthFull, bugReportButtonHeight)))
            {
                Util.OpenLink("https://github.com/rail2025/AetherBlackbox/issues");
            }
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Opens the GitHub Issues page in your browser.");
        }

        ImGui.Spacing();

        string buttonText = "Donate & Support development of the Replay & Planning features.";

        var buttonColor = new Vector4(0.9f, 0.2f, 0.2f, 1.0f);

        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, buttonColor * 1.2f);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, buttonColor * 0.8f);

        if (ImGui.Button(buttonText, new Vector2(btnWidthFull, 0)))
        {
            Util.OpenLink("https://ko-fi.com/rail2025");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Buy me a coffee!");
        }

        ImGui.PopStyleColor(3);
    }
}