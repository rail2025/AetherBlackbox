using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using AetherBlackbox.DrawingLogic;

namespace AetherBlackbox.Windows.Properties
{
    internal static class LayerList
    {
        private static Guid? renamingId;
        private static string renamingBuffer = "";

        public static unsafe void Draw(MainWindow mainWindow)
        {
            ImGui.Separator();
            ImGui.Text("Layers");

            var drawables = mainWindow.PageManager.GetCurrentPageDrawables();

            if (!ImGui.BeginChild("##Layers", new Vector2(0, 0), true))
                return;

            for (int i = drawables.Count - 1; i >= 0; i--)
            {
                var item = drawables[i];

                ImGui.PushID(item.UniqueId.ToString());

                bool selected = mainWindow.SelectedDrawables.Contains(item);
                string name = string.IsNullOrEmpty(item.Name)
                    ? item.ObjectDrawMode.ToString()
                    : item.Name;

                if (renamingId == item.UniqueId)
                {
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                    if (ImGui.InputText("##rename", ref renamingBuffer, 64,
                        ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        item.Name = renamingBuffer;
                        renamingId = null;

                        mainWindow.CanvasController.UndoManager.RecordAction(
                            drawables,
                            "Rename Object");

                        mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                            new List<BaseDrawable> { item });
                    }

                    if (!ImGui.IsItemActive() && ImGui.IsItemDeactivated())
                        renamingId = null;
                }
                else
                {
                    if (ImGui.Selectable($"{name}##{i}", selected))
                    {
                        if (ImGui.GetIO().KeyCtrl)
                        {
                            if (selected)
                                mainWindow.SelectedDrawables.Remove(item);
                            else
                                mainWindow.SelectedDrawables.Add(item);
                        }
                        else
                        {
                            mainWindow.SelectedDrawables.Clear();
                            mainWindow.SelectedDrawables.Add(item);
                        }
                    }

                    if (ImGui.IsItemHovered() &&
                        ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        renamingId = item.UniqueId;
                        renamingBuffer = item.Name ?? item.ObjectDrawMode.ToString();
                    }

                    if (ImGui.BeginDragDropSource())
                    {
                        int index = i;
                        ImGui.SetDragDropPayload("LAYER", new ReadOnlySpan<byte>(&index, sizeof(int)));
                        ImGui.Text(name);
                        ImGui.EndDragDropSource();
                    }

                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("LAYER");

                        if (payload.Data != null)
                        {
                            int from = System.BitConverter.ToInt32(
                                new ReadOnlySpan<byte>(payload.Data, sizeof(int)));

                            if (from != i)
                            {
                                var itemToMove = drawables[from];

                                drawables.RemoveAt(from);
                                drawables.Insert(i, itemToMove);

                                mainWindow.CanvasController.UndoManager.RecordAction(
                                    drawables,
                                    "Reorder Layer");

                                ImGui.EndDragDropTarget();
                                ImGui.PopID();
                                ImGui.EndChild();
                                return;
                            }
                        }

                        ImGui.EndDragDropTarget();
                    }
                }

                ImGui.PopID();
            }

            ImGui.EndChild();
        }
    }
}