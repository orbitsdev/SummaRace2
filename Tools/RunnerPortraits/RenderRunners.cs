using UnityEngine;
using UnityEditor;

public static class RenderRunners
{
    /// <summary>Import both runners' normal maps AS normal maps (they were plain colour textures).</summary>
    public static string FixNormals()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in new[] { "Assets/Art/Characters/Textures/Ch29_1001_Normal.png", "Assets/Art/Characters/Textures/Boy01_normal.jpg" })
        {
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null) { sb.Append(p).Append(" missing; "); continue; }
            sb.Append(p).Append(" was=").Append(imp.textureType);
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            sb.Append(" now=").Append(imp.textureType).Append("; ");
        }
        return sb.ToString();
    }

    public static string Render()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/Characters/Cat/character.prefab");
        var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Characters/Animations/Idle.fbx");
        AnimationClip clip = null;
        foreach (var a in idle) if (a is AnimationClip c && !c.name.StartsWith("__preview")) { clip = c; break; }

        var root = (GameObject)Object.Instantiate(prefab);
        root.transform.position = new Vector3(0, -500, 0);
        root.hideFlags = HideFlags.DontSave;

        var lightGo = new GameObject("~key") { hideFlags = HideFlags.DontSave };
        var key = lightGo.AddComponent<Light>();
        key.type = LightType.Directional; key.intensity = 1.15f; key.color = new Color(1f, 0.97f, 0.92f);
        lightGo.transform.rotation = Quaternion.Euler(25f, 160f, 0f);
        var fillGo = new GameObject("~fill") { hideFlags = HideFlags.DontSave };
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = 0.45f; fill.color = new Color(0.85f, 0.9f, 1f);
        fillGo.transform.rotation = Quaternion.Euler(10f, 210f, 0f);

        var camGo = new GameObject("~cam") { hideFlags = HideFlags.DontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.fieldOfView = 24f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;

        var ambientBefore = RenderSettings.ambientLight;
        var ambientModeBefore = RenderSettings.ambientMode;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.55f);

        var sb = new System.Text.StringBuilder("clip=" + (clip != null ? clip.name : "none") + " ");
        var rt = new RenderTexture(640, 840, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        try
        {
            string[] models = { "KidModel", "GirlModel" };
            for (int i = 0; i < 2; i++)
            {
                GameObject model = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "KidModel" || t.name == "GirlModel") t.gameObject.SetActive(t.name == models[i]);
                    if (t.name == models[i]) model = t.gameObject;
                }
                if (model == null) { sb.Append(models[i]).Append(" not found; "); continue; }

                var animator = model.GetComponentInChildren<Animator>(true);
                var target = animator != null ? animator.gameObject : model;
                if (clip != null)
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(target, clip, 0.8f);
                    AnimationMode.EndSampling();
                }

                var bounds = new Bounds(model.transform.position, Vector3.zero);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                float h = bounds.size.y;
                // Front, a touch above centre, turned 20deg for a friendlier three-quarter view.
                var facing = model.transform.forward;
                var dir = Quaternion.Euler(0, 20f, 0) * facing;
                float dist = (h * 0.51f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cam.transform.position = bounds.center + dir * dist + Vector3.up * h * 0.04f;
                cam.transform.LookAt(bounds.center + Vector3.up * h * 0.02f);
                key.transform.rotation = Quaternion.LookRotation(-(dir + Vector3.down * 0.4f + Quaternion.Euler(0, 40, 0) * dir * 0.5f));
                fill.transform.rotation = Quaternion.LookRotation(-(Quaternion.Euler(0, -60, 0) * dir));

                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;

                // Downscale to the 320x420 the card was designed around.
                var small = new RenderTexture(320, 420, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, small);
                RenderTexture.active = small;
                var outTex = new Texture2D(320, 420, TextureFormat.RGBA32, false);
                outTex.ReadPixels(new Rect(0, 0, 320, 420), 0, 0);
                outTex.Apply();
                RenderTexture.active = prev;
                System.IO.File.WriteAllBytes("Captures/runner_render_" + i + ".png", outTex.EncodeToPNG());
                small.Release();

                if (clip != null) AnimationMode.StopAnimationMode();
                sb.Append(models[i]).Append(" h=").Append(h.ToString("0.00")).Append(" ok; ");
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            RenderSettings.ambientLight = ambientBefore;
            RenderSettings.ambientMode = ambientModeBefore;
            rt.Release();
            Object.DestroyImmediate(root); Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo); Object.DestroyImmediate(fillGo);
        }
        return sb.ToString();
    }
}
