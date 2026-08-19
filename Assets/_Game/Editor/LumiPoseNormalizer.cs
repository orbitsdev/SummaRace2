// LumiPoseNormalizer.cs — makes every Ms. Lumi pose the SAME Ms. Lumi.
//
// Menu: SummaRace ▸ Normalize Lumi Poses   (also runs automatically on import)
//
// WHY THIS EXISTS
// ---------------
// The pose PNGs in Resources/UI/Lumi are tightly cropped to their own content, and each
// one was drawn at whatever zoom its gesture needed. Measured across the 23 poses that
// shipped first, her HEAD is 99px wide in lumi_shrug (arms flung out) and 146px wide in
// idle_happy (head and shoulders only) — a 1.47x spread in the source art.
//
// A UI Image with preserveAspect makes that far worse, because it fits the whole CANVAS
// to the box: cheer_hooray is 305x225 and cheer_laugh is 194x241, so dropped into one
// square box the first fits by width and the second by height, and her head changes size
// by 2.8x between two poses that are supposed to be the same person. She would visibly
// balloon and shrink every time she changed expression, which reads as broken rather
// than alive.
//
// THE FIX, AND WHY IT LIVES ON THE IMPORTER
// -----------------------------------------
// This tool measures her head in each PNG and writes two numbers onto the sprite's own
// importer, so the answer travels with the asset instead of sitting in a lookup table
// that a newly dropped file would not be in:
//
//   pixelsPerUnit := the measured head width in pixels.
//       That makes "sprite.rect.size / sprite.pixelsPerUnit" the sprite's size expressed
//       in HEAD-WIDTHS, so MsLumiReactor can say "her head is 237 pixels" and every pose
//       obeys, regardless of how much empty arm-span its own crop happens to include.
//
//   pivot := the centre of her head.
//       MsLumiReactor copies this onto the RectTransform, so the rect's anchored position
//       means "where her face is". Her face then stays put while arms and body change
//       around it, instead of the whole image re-centring on every swap.
//
// Head width is the right yardstick rather than image height or alpha bounds: every pose
// is a head-and-shoulders portrait, so the head is the one feature present in all of them
// and the one unaffected by whether she is waving, shrugging or standing still.
//
// HOW THE HEAD IS FOUND
// ---------------------
// Her hair is a flat dark brown (~52,38,31) that appears nowhere else on her — skin is a
// light tan, the sweater is green, the outline is near-black. So the head is simply the
// bounding box of the hair-coloured pixels. The mask is eroded twice before measuring,
// because the anti-aliased seam between the black outline and the skin passes through
// mid-browns; without erosion those stray edge pixels along an outstretched ARM land in
// the mask and lumi_shrug reports a 327px-wide "head" (the whole canvas) instead of 99px.
//
// The measurement is skipped, leaving the importer untouched, when no hair is found or
// the result is implausible — a mis-drawn or non-Lumi file should do nothing, not be
// sized by a wrong number.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SummaRace.EditorTools
{
    public static class LumiPoseNormalizer
    {
        /// <summary>Folder scanned. Mirrors LumiExpressions.FolderPath.</summary>
        public const string FolderAssetPath = "Assets/_Game/Resources/UI/Lumi";

        // Hair colour gate. Deliberately loose on brightness (shading) and tight on hue
        // ordering (r > g > b by a margin), which is what separates brown hair from both
        // the near-black outline and the warm skin tone.
        private const byte MinAlpha = 230;
        private const byte RMin = 38, RMax = 80;
        private const byte GMin = 26, GMax = 58;
        private const byte BMin = 20, BMax = 52;
        private const int ROverG = 8;
        private const int GOverB = 3;
        private const int ErodePasses = 2;

        // A head narrower than this is almost certainly not a head; wider than this means
        // the mask leaked onto an arm. Either way: leave the importer alone and say so.
        private const int MinHeadPx = 40;
        private const float MaxHeadFractionOfWidth = 0.92f;

        [MenuItem("SummaRace/Normalize Lumi Poses")]
        public static void RunFromMenu()
        {
            int changed = Run(out var report);
            Debug.Log(report);
            EditorUtility.DisplayDialog(
                "Normalize Lumi Poses",
                changed == 0
                    ? "Every pose was already normalized — nothing to do.\n\nSee the Console for the measured table."
                    : "Updated " + changed + " pose(s).\n\nSee the Console for the measured table.",
                "OK");
        }

        /// <summary>
        /// Measures every pose and writes pivot + pixelsPerUnit where they differ.
        /// Returns how many importers were actually changed.
        /// </summary>
        public static int Run(out string report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[LumiPoseNormalizer] " + FolderAssetPath);
            sb.AppendLine(string.Format("{0,-24}{1,10}{2,9}{3,18}   result",
                "pose", "canvas", "head px", "pivot"));

            if (!AssetDatabase.IsValidFolder(FolderAssetPath))
            {
                sb.AppendLine("  folder does not exist — nothing to do.");
                report = sb.ToString();
                return 0;
            }

            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FolderAssetPath }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);

            int changed = 0;
            foreach (var path in paths)
                if (Normalize(path, sb)) changed++;

            sb.AppendLine("  " + paths.Count + " pose(s) scanned, " + changed + " importer(s) updated.");
            report = sb.ToString();
            return changed;
        }

        private static bool Normalize(string assetPath, System.Text.StringBuilder sb)
        {
            var name = Path.GetFileNameWithoutExtension(assetPath);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                sb.AppendLine(string.Format("{0,-24}{1,10}{2,9}{3,18}   skipped (not a texture)",
                    name, "", "", ""));
                return false;
            }

            // Read the file off disk rather than the imported Texture2D: the imported one is
            // not readable (isReadable: 0, which is correct for shipping) and a compressed
            // format would smear the flat hair colour the mask depends on.
            Texture2D tex = null;
            try
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(assetPath), false))
                {
                    sb.AppendLine(string.Format("{0,-24}{1,10}{2,9}{3,18}   skipped (could not decode)",
                        name, "", "", ""));
                    return false;
                }

                var canvas = tex.width + "x" + tex.height;

                Vector2Int headMin, headMax;
                if (!MeasureHead(tex, out headMin, out headMax))
                {
                    sb.AppendLine(string.Format("{0,-24}{1,10}{2,9}{3,18}   skipped (no hair found)",
                        name, canvas, "", ""));
                    return false;
                }

                float headWidth = headMax.x - headMin.x + 1f;
                if (headWidth < MinHeadPx || headWidth > tex.width * MaxHeadFractionOfWidth)
                {
                    sb.AppendLine(string.Format("{0,-24}{1,10}{2,9:0}{3,18}   skipped (implausible)",
                        name, canvas, headWidth, ""));
                    return false;
                }

                // GetPixels32 rows run bottom-up, which is already Unity sprite space, so the
                // measured centre needs no flip.
                var pivot = new Vector2(
                    (headMin.x + headMax.x + 1f) * 0.5f / tex.width,
                    (headMin.y + headMax.y + 1f) * 0.5f / tex.height);

                return Apply(importer, headWidth, pivot, name, canvas, sb);
            }
            finally
            {
                if (tex != null) Object.DestroyImmediate(tex);
            }
        }

        private static bool Apply(TextureImporter importer, float headWidth, Vector2 pivot,
                                  string name, string canvas, System.Text.StringBuilder sb)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            bool already =
                settings.textureType == TextureImporterType.Sprite &&
                settings.spriteAlignment == (int)SpriteAlignment.Custom &&
                Mathf.Abs(settings.spritePixelsPerUnit - headWidth) < 0.01f &&
                (settings.spritePivot - pivot).sqrMagnitude < 1e-8f &&
                settings.spriteMeshType == SpriteMeshType.FullRect;

            var pivotText = string.Format("({0:0.000},{1:0.000})", pivot.x, pivot.y);
            if (already)
            {
                sb.AppendLine(string.Format("{0,-24}{1,10}{2,9:0}{3,18}   ok",
                    name, canvas, headWidth, pivotText));
                return false;
            }

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spritePixelsPerUnit = headWidth;
            // FullRect, not Tight: a tight mesh re-crops the quad to the opaque pixels, which
            // would undo the pivot just measured against the full canvas.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.alphaIsTransparency = true;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            sb.AppendLine(string.Format("{0,-24}{1,10}{2,9:0}{3,18}   UPDATED",
                name, canvas, headWidth, pivotText));
            return true;
        }

        /// <summary>
        /// Bounding box of the eroded hair mask, in pixels with (0,0) bottom-left.
        /// </summary>
        private static bool MeasureHead(Texture2D tex, out Vector2Int min, out Vector2Int max)
        {
            min = default(Vector2Int);
            max = default(Vector2Int);
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();

            var mask = new bool[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                mask[i] =
                    c.a >= MinAlpha &&
                    c.r >= RMin && c.r <= RMax &&
                    c.g >= GMin && c.g <= GMax &&
                    c.b >= BMin && c.b <= BMax &&
                    c.r > c.g + ROverG &&
                    c.g > c.b + GOverB;
            }

            var scratch = new bool[w * h];
            for (int pass = 0; pass < ErodePasses; pass++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (!mask[i]) { scratch[i] = false; continue; }
                        // A border pixel cannot have a full neighbourhood, so it erodes away.
                        if (x == 0 || y == 0 || x == w - 1 || y == h - 1) { scratch[i] = false; continue; }
                        scratch[i] =
                            mask[i - 1] && mask[i + 1] &&
                            mask[i - w] && mask[i + w] &&
                            mask[i - w - 1] && mask[i - w + 1] &&
                            mask[i + w - 1] && mask[i + w + 1];
                    }
                }
                System.Array.Copy(scratch, mask, mask.Length);
            }

            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!mask[y * w + x]) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return false;

            min = new Vector2Int(minX, minY);
            max = new Vector2Int(maxX, maxY);
            return true;
        }

        /// <summary>
        /// Keeps "drop a PNG in the folder and it works" literally true — the same rule the
        /// hero art and the narration already follow. Guarded by the value comparison in
        /// Apply(), so a pose that is already correct triggers no further import and this
        /// cannot loop.
        /// </summary>
        private sealed class ImportHook : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                                       string[] moved, string[] movedFrom)
            {
                if (!TouchesLumiFolder(imported) && !TouchesLumiFolder(moved)) return;
                string report;
                if (Run(out report) > 0) Debug.Log(report);
            }

            private static bool TouchesLumiFolder(string[] paths)
            {
                if (paths == null) return false;
                for (int i = 0; i < paths.Length; i++)
                    if (paths[i] != null &&
                        paths[i].StartsWith(FolderAssetPath, System.StringComparison.OrdinalIgnoreCase) &&
                        paths[i].EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }
    }
}
