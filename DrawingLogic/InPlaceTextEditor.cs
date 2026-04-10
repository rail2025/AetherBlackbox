using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBlackbox.Core;
using AetherBlackbox.Networking;
using AetherBlackbox.Serialization;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;




namespace AetherBlackbox.DrawingLogic
{
    public class InPlaceTextEditor
    {
        private readonly Plugin plugin;
        private readonly UndoManager undoManager;
        private readonly PageManager pageManager;

        public bool IsEditing { get; private set; } = false;
        private DrawableText? targetTextObject_;
        private string originalText_ = string.Empty;
        private float originalFontSize_;
        private string editTextBuffer_ = string.Empty;
        private Vector2 editorWindowPosition_;
        private bool shouldSetFocus_ = false;

        private const int MaxBufferSize = 2048;
        private bool p_open = true;

        public InPlaceTextEditor(Plugin pluginInstance, UndoManager undoManagerInstance, PageManager pageManagerInstance)
        {
            this.plugin = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            this.undoManager = undoManagerInstance ?? throw new ArgumentNullException(nameof(undoManagerInstance));
            this.pageManager = pageManagerInstance ?? throw new ArgumentNullException(nameof(pageManagerInstance));
        }

        public void BeginEdit(DrawableText textObject, Vector2 canvasOriginScreen, float currentGlobalScale)
        {
            if (textObject == null) return;

            IsEditing = true;
            targetTextObject_ = textObject;
            originalText_ = textObject.RawText;
            originalFontSize_ = textObject.FontSize;
            editTextBuffer_ = textObject.RawText ?? string.Empty;

            if (editTextBuffer_.Length > MaxBufferSize)
                editTextBuffer_ = editTextBuffer_.Substring(0, MaxBufferSize);

            shouldSetFocus_ = true;
            RecalculateEditorBounds(canvasOriginScreen, currentGlobalScale);
        }

        public void RecalculateEditorBounds(Vector2 canvasOriginScreen, float currentGlobalScale)
        {
            if (targetTextObject_ == null) return;
            editorWindowPosition_ = (targetTextObject_.PositionRelative * currentGlobalScale) + canvasOriginScreen;
        }

        public void DrawEditorUI()
        {
            if (!IsEditing || targetTextObject_ == null) return;

            ImGui.SetNextWindowPos(editorWindowPosition_);
            ImGui.SetNextWindowSizeConstraints(new Vector2(200, 100) * ImGuiHelpers.GlobalScale, new Vector2(800, 600) * ImGuiHelpers.GlobalScale);
            ImGui.SetNextWindowSize(new Vector2(300, 200) * ImGuiHelpers.GlobalScale, ImGuiCond.Appearing);


            string windowId = $"##TextEditorWindow_{targetTextObject_.GetHashCodeShort()}";
            ImGuiWindowFlags editorFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8) * ImGuiHelpers.GlobalScale);

            if (ImGui.Begin(windowId, ref p_open, editorFlags))
            {
                var activeColor = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];
                var fontSizes = new[] { ("S", 12f), ("M", 20f), ("L", 32f), ("XL", 48f) };

                for (int i = 0; i < fontSizes.Length; i++)
                {
                    var (label, size) = fontSizes[i];
                    bool isSelected = Math.Abs(targetTextObject_.FontSize - size) < 0.01f;

                    using (isSelected ? ImRaii.PushColor(ImGuiCol.Button, activeColor) : null)
                    {
                        if (ImGui.Button(label))
                        {
                            targetTextObject_.FontSize = size;
                        }
                    }
                    if (i < fontSizes.Length - 1) ImGui.SameLine();
                }
                ImGui.Separator();

                ImGui.InputTextMultiline("##EditText", ref editTextBuffer_, MaxBufferSize, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight() * 5));

                if (shouldSetFocus_)
                {
                    ImGui.SetKeyboardFocusHere(-1);
                    shouldSetFocus_ = false;
                }

                bool committed = false;
                bool canceled = false;
                if (ImGui.Button("OK")) committed = true; ImGui.SameLine();
                if (ImGui.Button("Cancel")) canceled = true;
                if (ImGui.IsKeyPressed(ImGuiKey.Escape)) canceled = true;

                if (committed) CommitAndEndEdit();
                else if (canceled) CancelAndEndEdit();
            }
            ImGui.End();
            ImGui.PopStyleVar();
        }

        public void CommitAndEndEdit()
        {
            if (!IsEditing || targetTextObject_ == null) return;

            bool textChanged = originalText_ != editTextBuffer_;
            bool fontChanged = Math.Abs(originalFontSize_ - targetTextObject_.FontSize) > 0.01f;

            // Update the local object first
            targetTextObject_.RawText = editTextBuffer_;

            if (textChanged || fontChanged)
            {
                // Record the action for local undo
                undoManager.RecordAction(pageManager.GetCurrentPageDrawables(), "Edit Text");

                // If in a live session, send the update to other clients
                if (pageManager.IsLiveMode)
                {
                    var payload = new NetworkPayload
                    {
                        PageIndex = pageManager.GetCurrentPageIndex(),
                        Action = PayloadActionType.UpdateObjects,
                        Data = DrawableSerializer.SerializePageToBytes(new List<BaseDrawable> { targetTextObject_ })
                    };
                    _ = plugin.NetworkManager.SendStateUpdateAsync(payload);
                }
            }

            CleanUpEditSession();
        }

        public void CancelAndEndEdit()
        {
            if (!IsEditing || targetTextObject_ == null) return;

            targetTextObject_.RawText = originalText_;
            targetTextObject_.FontSize = originalFontSize_;
            CleanUpEditSession();
        }

        private void CleanUpEditSession()
        {
            IsEditing = false;
            targetTextObject_ = null;
        }

        public bool IsCurrentlyEditing(BaseDrawable? drawable)
        {
            return IsEditing && targetTextObject_ != null && ReferenceEquals(targetTextObject_, drawable);
        }
    }
}
