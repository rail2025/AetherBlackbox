using AetherBlackbox.DrawingLogic;
using AetherBlackbox.Windows;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AetherBlackbox.Core
{
    public class UndoAction
    {
        public List<BaseDrawable> PreviousDrawablesState { get; private set; }

        public string Description { get; private set; }

        public UndoAction(List<BaseDrawable> drawablesStateToSave, string description)
        {
            PreviousDrawablesState = new List<BaseDrawable>();
            foreach (var drawable in drawablesStateToSave)
            {
                var cloned = drawable.Clone();
                if (cloned != null)
                {
                    PreviousDrawablesState.Add(cloned);
                }
            }
            Description = description;
        }
    }

    public class UndoManager
    {
        private List<Stack<UndoAction>> undoStacks = new List<Stack<UndoAction>>();
        private int activeStackIndex = 0;
        private const int MaxUndoLevels = 30; // Arbitrary limit for undo history

        private void EnsureStacks(int count)
        {
            while (undoStacks.Count < count)
            {
                undoStacks.Add(new Stack<UndoAction>());
            }
        }

        public void InitializeStacks(int pageCount)
        {
            undoStacks.Clear();
            for (int i = 0; i < pageCount; i++)
            {
                undoStacks.Add(new Stack<UndoAction>());
            }
            activeStackIndex = 0;
        }

        public void SetActivePage(int index)
        {
            EnsureStacks(index + 1);

            if (index < 0 || index >= undoStacks.Count)
            {
                return;
            }
            activeStackIndex = index;
        }

        public void AddStack(int index)
        {
            if (index < 0 || index > undoStacks.Count)
            {
                return;
            }
            undoStacks.Insert(index, new Stack<UndoAction>());
        }

        public void RemoveStack(int index)
        {
            if (index < 0 || index >= undoStacks.Count)
            {
                return;
            }
            undoStacks.RemoveAt(index);
            if (activeStackIndex >= index)
            {
                activeStackIndex = Math.Max(0, activeStackIndex - 1);
            }
        }

        public void MoveStack(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= undoStacks.Count || toIndex < 0 || toIndex >= undoStacks.Count)
            {
                return;
            }
            var stackToMove = undoStacks[fromIndex];
            undoStacks.RemoveAt(fromIndex);
            undoStacks.Insert(toIndex, stackToMove);
        }

        public void RecordAction(List<BaseDrawable> currentDrawables, string actionDescription)
        {
            EnsureStacks(activeStackIndex + 1);

            if (undoStacks.Count == 0 || activeStackIndex < 0 || activeStackIndex >= undoStacks.Count)
            {
                return;
            }

            var activeStack = undoStacks[activeStackIndex];

            if (activeStack.Count > 0)
            {
                var lastAction = activeStack.Peek();

                var validCurrentCount = currentDrawables.Count(d => d.ObjectDrawMode != DrawMode.Laser);

                if (validCurrentCount == lastAction.PreviousDrawablesState.Count)
                {
                    var currentSaveState = currentDrawables
                        .Select(d => d.Clone())
                        .Where(d => d != null)
                        .ToList();

                    string currentJson = JsonSerializer.Serialize(currentSaveState);
                    string lastJson = JsonSerializer.Serialize(lastAction.PreviousDrawablesState);

                    if (currentJson == lastJson)
                    {
                        return;
                    }
                }
            }

            if (activeStack.Count >= MaxUndoLevels)
            {
                TrimOldestUndo(activeStack);
            }

            var action = new UndoAction(currentDrawables, actionDescription);

            activeStack.Push(action);
        }

        public List<BaseDrawable>? Undo()
        {
            if (CanUndo())
            {
                var activeStack = undoStacks[activeStackIndex];
                UndoAction lastAction = activeStack.Pop();
                return lastAction.PreviousDrawablesState;
            }
            return null;
        }

        public bool CanUndo()
        {
            return undoStacks.Count > 0 && activeStackIndex >= 0 && activeStackIndex < undoStacks.Count && undoStacks[activeStackIndex].Count > 0;
        }

        public void ClearHistory()
        {
            undoStacks.Clear();
            activeStackIndex = 0;
        }

        private void TrimOldestUndo(Stack<UndoAction> activeStack)
        {
            if (activeStack.Count >= MaxUndoLevels)
            {
                var tempList = activeStack.ToList();
                while (tempList.Count >= MaxUndoLevels)
                {
                    tempList.RemoveAt(tempList.Count - 1);
                }
                activeStack.Clear();
                for (int i = tempList.Count - 1; i >= 0; i--)
                {
                    activeStack.Push(tempList[i]);
                }
            }
        }
    }
}
