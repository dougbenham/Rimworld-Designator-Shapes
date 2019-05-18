﻿using Merthsoft.DesignatorShapes.Defs;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Merthsoft.DesignatorShapes {
    public class ShapeControls {
        private List<OverlayGroupDef> groupDefs => DefDatabase<OverlayGroupDef>.AllDefsListForReading;

        public Rect WindowRect { get; set; }
        public Rect DraggingWindowRect => new Rect(WindowRect.x - 100, WindowRect.y - 100, WindowRect.width + 200, WindowRect.height + 200);

        public bool IsHorizontal { get; set; }

        public OverlayGroupDef SelectedGroup { get; set; }
        private Stack<OverlayGroupDef> previousGroups;

        private const int ID = 12341234;

        public static int IconSize => DesignatorShapes.Settings.IconSize;
        public static int NumButtons = 8;
        public static int NumRows = 2;
        public static int LabelHeight = 20;
        public static int LineHeight = 3;

        private bool dragging;

        public static int Width => NumButtons / NumRows * IconSize;
        public static int Height => NumRows * IconSize + LabelHeight + LineHeight;

        private int inputWidth;
        private int inputHeight;

        public static IntVec3 InputVec => new IntVec3(DesignatorShapes.ShapeControls.inputWidth, 0, DesignatorShapes.ShapeControls.inputHeight);

        public ShapeControls(int x, int y) {
            IsHorizontal = true;
            previousGroups = new Stack<OverlayGroupDef>();

            WindowRect = new Rect(x, y, Width, Height);
        }

        public void ShapeControlsOnGUI() {
            if (Find.MainTabsRoot.OpenTab == null) { return; }

            if (WindowRect.x == -1) {
                Rect infoRect = ArchitectCategoryTab.InfoRect;
                WindowRect = new Rect(infoRect.x, infoRect.y - 110, Width, Height);
            }

            Find.WindowStack.ImmediateWindow(ID, dragging ? DraggingWindowRect : WindowRect, WindowLayer.GameUI, DoWindow, false, dragging, 0);
        }
        
        private void drawIcon(Rect rect, Texture2D icon, Color? highlighColor, Action action) {
            if (Widgets.ButtonImage(rect, icon, Color.white, highlighColor ?? GenUI.MouseoverColor)) {
                action.Invoke();
            }
        }

        private void drawIcons(Rect rect, List<ActionIcon> icons) {
            var x = 0;
            var y = 0;
            foreach (var icon in icons) {
                drawIcon(new Rect(x * IconSize + rect.x, y * IconSize + rect.y, IconSize, IconSize), icon.icon, icon.highlightColor, icon.action);

                x++;
                if (x == NumButtons / NumRows) {
                    x = 0;
                    y++;
                }
            }
        }

        private void DoWindow() {
            var offset = dragging ? 100 : 0;

            var rect = new Rect(offset, offset, Width, Height);
            if (DesignatorShapes.Settings.DrawBackground) {
                Widgets.DrawWindowBackground(rect);
            }

            Widgets.Label(rect, "Shapes");
            Widgets.DrawLineHorizontal(rect.x + 3, rect.y + LabelHeight, rect.width - 6);
            rect.y += LabelHeight + LineHeight;
            rect.height -= LabelHeight;
            drawIcons(rect, generateIcons());

            if (DesignatorShapes.CurrentTool?.Group == SelectedGroup 
                && (DesignatorShapes.CurrentTool?.useSizeInputs ?? false)) { 
                var buffer = inputWidth.ToString();

                rect.y += IconSize + 10;
                rect.width /= 2;
                rect.width -= 25;
                rect.height = 20;

                Widgets.Label(rect, "W");
                rect.x += 20;
                rect.width -= 20;
                Widgets.TextFieldNumeric(rect, ref inputWidth, ref buffer);
                rect.x += rect.width + 25;
                Widgets.Label(rect, "H");
                rect.x += 20;
                buffer = inputHeight.ToString();
                Widgets.TextFieldNumeric(rect, ref inputHeight, ref buffer);

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab) {
                    GUI.FocusWindow(ID);
                    Event.current.Use();
                }
            }

            if (Event.current.isMouse) {
                switch (Event.current.type) {
                    case EventType.MouseDown:
                        dragging = true;
                        Event.current.Use();
                        break;
                    case EventType.MouseDrag:
                        WindowRect = new Rect(WindowRect.x + Event.current.delta.x, WindowRect.y + Event.current.delta.y, Width, Height);
                        Event.current.Use();
                        break;
                    case EventType.MouseUp:
                        dragging = false;
                        DesignatorShapes.Settings.WindowX = (int)WindowRect.x;
                        DesignatorShapes.Settings.WindowY = (int)WindowRect.y;
                        DesignatorShapes.Settings.Write();
                        Event.current.Use();
                        break;
                }
            }
        }

        private List<ActionIcon> generateIcons() {
            List<ActionIcon> icons;
            if (SelectedGroup == null) {
                icons = groupDefs.Where(g => g.ParentGroup == null).SelectList(CreateActionIcon);
                icons.Add(new ActionIcon {
                    icon = HistoryManager.CanUndo ? Icons.UndoEnabled : Icons.UndoDisabled,
                    action = HistoryManager.Undo,
                    highlightColor = HistoryManager.CanUndo ? GenUI.MouseoverColor : Color.grey
                });
                icons.Add(new ActionIcon {
                    icon = HistoryManager.CanRedo ? Icons.RedoEnabled : Icons.RedoDisabled,
                    action = HistoryManager.Redo,
                    highlightColor = HistoryManager.CanRedo ? GenUI.MouseoverColor : Color.grey
                });
            } else {
                icons = new List<ActionIcon> { new ActionIcon {
                    action = () => SelectedGroup = previousGroups.Pop(),
                    icon = SelectedGroup.CloseUiIcon
                } };
                icons.AddRange(SelectedGroup.Shapes.Select(s => new ActionIcon {
                    icon = DesignatorShapes.CurrentTool.defName == s.defName ? s.selectedUiIcon : s.uiIcon,
                    action = () => DesignatorShapes.SelectTool(s)
                }));
                icons.AddRange(SelectedGroup.ChildrenGroups.SelectList(CreateActionIcon));
            }

            return icons;
        }

        static ActionIcon CreateActionIcon(OverlayGroupDef g) => new ActionIcon {
            icon = DesignatorShapes.CurrentTool?.Group.defName == g.defName
                    ? DesignatorShapes.CurrentTool.selectedUiIcon
                    : g.UiIcon,
            action = DesignatorShapes.ShapeControls.GetAction(g)
        };

        Action GetAction(OverlayGroupDef g) {
            if (g.NumShapes == 1) {
                return () => DesignatorShapes.SelectTool(g.FirstShape);
            } else {
                if (DesignatorShapes.Settings.UseSubMenus) {
                    return () => {
                        previousGroups.Push(SelectedGroup);
                        SelectedGroup = g;
                    };
                } else {
                    return () => Find.WindowStack.Add(new FloatMenu(g.Shapes.SelectList(s =>
                        new FloatMenuOption(s.LabelCap, s.Select)
                    )));
                }
            }
        }
    }
}