public static class WarmGirl
{
    public static string Main(string r, string g, string b)
    {
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Art/Characters/Materials/Ch29_Body_Cutout.mat");
        var c = new UnityEngine.Color(float.Parse(r), float.Parse(g), float.Parse(b), 1f);
        mat.SetColor("_BaseColor", c);
        mat.SetFloat("_Smoothness", 0.15f); mat.SetFloat("_Cutoff", 0.9f);
        UnityEditor.EditorUtility.SetDirty(mat);
        UnityEditor.AssetDatabase.SaveAssets();
        return "base=" + c;
    }
}
