using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SummaRace.EditorTools
{
    /// <summary>
    /// SummaRace is portrait-locked, but the Editor's Game view defaults to a landscape
    /// desktop size. Every screen then renders squashed and overlay UI cannot be judged at
    /// all -- which is exactly the trap CLAUDE.md records under "Working with the Unity
    /// Editor" (every P2b/P3b layout bug was invisible until the canvas was rendered
    /// portrait).
    ///
    /// This registers the three portrait aspects the layout was authored and hardened
    /// against, and selects the reference one.
    ///
    ///   1080x1920 (9:16)  the canvas reference. CanvasScaler matches width, so the canvas
    ///                     is always 1080 wide and its height tracks the aspect. This is
    ///                     the aspect every screen was playtested at.
    ///   1200x1920 (16:10) common cheap Android tablet.
    ///   1440x1920 (4:3)   the tightest aspect, and the one where the reading band was
    ///                     measured covering 34px of the tracker and 50px of the pause chip
    ///                     before both were moved onto fractional anchors.
    ///
    /// Everything here is reflection over internal Editor types (GameViewSizes/GameViewSize
    /// have no public API). It is wrapped so a failure prints how to do it by hand instead
    /// of throwing.
    /// </summary>
    public static class PortraitGameView
    {
        private struct Preset
        {
            public string Label;
            public int Width;
            public int Height;

            public Preset(string label, int width, int height)
            {
                Label = label;
                Width = width;
                Height = height;
            }
        }

        private static readonly Preset[] Presets =
        {
            new Preset("SummaRace Portrait 9:16", 1080, 1920),
            new Preset("SummaRace Portrait 16:10", 1200, 1920),
            new Preset("SummaRace Portrait 4:3", 1440, 1920),
        };

        private const string ManualFallback =
            "Set it by hand instead: Game view -> the aspect dropdown (top-left) -> '+' -> " +
            "Type 'Fixed Resolution', Width 1080, Height 1920 -> Add.";

        [MenuItem("SummaRace/Portrait Game View", false, 20)]
        public static void Apply()
        {
            try
            {
                var editorAssembly = typeof(EditorWindow).Assembly;

                var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                var sizeTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                var gameViewType = editorAssembly.GetType("UnityEditor.GameView");

                if (sizesType == null || sizeType == null || sizeTypeEnum == null || gameViewType == null)
                {
                    Debug.LogWarning("[PortraitGameView] Unity's internal Game view types moved in this " +
                                     "editor version. " + ManualFallback);
                    return;
                }

                // GameViewSizes is a ScriptableSingleton<GameViewSizes>.
                var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singletonType
                    .GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null, null);

                // The property has been spelled both ways across versions.
                var groupTypeProp = sizesType.GetProperty("currentGroupType")
                                    ?? sizesType.GetProperty("CurrentGroupType");
                var currentGroupType = groupTypeProp.GetValue(instance, null);

                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { currentGroupType });
                var groupType = group.GetType();

                var getTotalCount = groupType.GetMethod("GetTotalCount");
                var getGameViewSize = groupType.GetMethod("GetGameViewSize");
                var addCustomSize = groupType.GetMethod("AddCustomSize");
                var baseTextProp = sizeType.GetProperty("baseText");

                int selectIndex = -1;

                foreach (var preset in Presets)
                {
                    int index = IndexOf(group, getTotalCount, getGameViewSize, baseTextProp, preset.Label);

                    if (index < 0)
                    {
                        var ctor = sizeType.GetConstructor(new[]
                            { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });

                        var newSize = ctor.Invoke(new object[]
                        {
                            Enum.Parse(sizeTypeEnum, "FixedResolution"),
                            preset.Width, preset.Height, preset.Label,
                        });

                        addCustomSize.Invoke(group, new[] { newSize });
                        index = IndexOf(group, getTotalCount, getGameViewSize, baseTextProp, preset.Label);
                    }

                    // The first preset is the reference aspect, so that is the one selected.
                    if (selectIndex < 0) selectIndex = index;
                }

                if (selectIndex < 0)
                {
                    Debug.LogWarning("[PortraitGameView] Could not register a portrait size. " + ManualFallback);
                    return;
                }

                var saveToHdd = sizesType.GetMethod("SaveToHDD");
                if (saveToHdd != null) saveToHdd.Invoke(instance, null);

                // Selecting needs a Game view to exist. GetWindow will open one if it does not,
                // and focus is deliberately not stolen (last arg) so this can run mid-task.
                var gameView = EditorWindow.GetWindow(gameViewType, false, "Game", false);
                var select = gameViewType.GetMethod("SizeSelectionCallback",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (select == null)
                {
                    Debug.LogWarning("[PortraitGameView] Registered the portrait sizes, but this editor " +
                                     "version renamed the selection call -- pick 'SummaRace Portrait 9:16' " +
                                     "from the Game view's aspect dropdown yourself.");
                    return;
                }

                select.Invoke(gameView, new object[] { selectIndex, null });
                gameView.Repaint();

                Debug.Log("[PortraitGameView] Game view set to SummaRace Portrait 9:16 (1080x1920). " +
                          "Also registered 16:10 (1200x1920) and 4:3 (1440x1920) -- switch between them " +
                          "in the Game view's aspect dropdown to check a layout on tablet aspects.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PortraitGameView] " + e.GetType().Name + ": " + e.Message + ". " +
                                 ManualFallback);
            }
        }

        private static int IndexOf(object group, MethodInfo getTotalCount, MethodInfo getGameViewSize,
                                   PropertyInfo baseTextProp, string label)
        {
            int total = (int)getTotalCount.Invoke(group, null);
            for (int i = 0; i < total; i++)
            {
                var size = getGameViewSize.Invoke(group, new object[] { i });
                var text = baseTextProp.GetValue(size, null) as string;
                if (text == label) return i;
            }
            return -1;
        }
    }
}
