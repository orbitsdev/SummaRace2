using UnityEngine;
using UnityEditor;

public static class FixGirl
{
    const string MatPath = "Assets/Art/Characters/Materials/Ch29_Body_Cutout.mat";
    const string PrefabPath = "Assets/Bundles/Characters/Cat/character.prefab";

    public static string Main()
    {
        var sb = new System.Text.StringBuilder();
        Material src = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Characters/Ch29_nonPBR.fbx"))
            if (o is Material m && m.name == "Ch29_Body") src = m;
        if (src == null) return "no source material";

        if (!AssetDatabase.IsValidFolder("Assets/Art/Characters/Materials"))
            AssetDatabase.CreateFolder("Assets/Art/Characters", "Materials");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(src) { name = "Ch29_Body_Cutout" };
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        // The diffuse carries a see-through visor (alpha ~0 over pale blue-white). Rendered opaque
        // it was a white sheet over her face. Alpha CLIP (not transparency) keeps her in the
        // opaque queue: no sorting problems on a moving runner, and no extra cost on the tablet.
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Smoothness", 0.25f);   // less plastic shine on pale skin
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        int changed = 0;
        try
        {
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMaterial != null && r.sharedMaterial.name == "Ch29_Body")
                {
                    r.sharedMaterial = mat;
                    changed++;
                }
            }
            if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return sb.Append("material ok, renderers changed=").Append(changed).ToString();
    }
}
