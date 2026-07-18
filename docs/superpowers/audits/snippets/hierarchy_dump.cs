// Audit snippet: full hierarchy dump (incl. inactive) of the active scene.
// Run via mcp__UnityMCP__execute_code (CodeDom C#6). Substitute ___OUT___ with an
// absolute output path, e.g. C:\Users\Owner\Documents\GitHub\SummaRace2\docs\superpowers\audits\shots\<scene>_hierarchy.txt
var sb = new System.Text.StringBuilder();
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
sb.AppendLine("SCENE: " + scene.path);
var stack = new System.Collections.Generic.Stack<System.Collections.Generic.KeyValuePair<UnityEngine.Transform, int>>();
var roots = scene.GetRootGameObjects();
for (int r = roots.Length - 1; r >= 0; r--)
    stack.Push(new System.Collections.Generic.KeyValuePair<UnityEngine.Transform, int>(roots[r].transform, 0));
while (stack.Count > 0)
{
    var pair = stack.Pop();
    var t = pair.Key; int depth = pair.Value;
    var go = t.gameObject;
    var comps = go.GetComponents<UnityEngine.Component>();
    var names = new System.Collections.Generic.List<string>();
    foreach (var c in comps) names.Add(c == null ? "MISSING_SCRIPT" : c.GetType().Name);
    string pad = new string(' ', depth * 2);
    sb.AppendLine(pad + (go.activeInHierarchy ? "" : "[INACTIVE] ") + go.name + "  <" + string.Join(",", names.ToArray()) + ">");
    var tmp = go.GetComponent<TMPro.TMP_Text>();
    if (tmp != null) sb.AppendLine(pad + "  TEXT=\"" + tmp.text.Replace("\n", "\\n") + "\" font=" + (tmp.font != null ? tmp.font.name : "NULL"));
    var legacy = go.GetComponent<UnityEngine.TextMesh>();
    if (legacy != null) sb.AppendLine(pad + "  LEGACYTEXT=\"" + legacy.text + "\"");
    for (int i = t.childCount - 1; i >= 0; i--)
        stack.Push(new System.Collections.Generic.KeyValuePair<UnityEngine.Transform, int>(t.GetChild(i), depth + 1));
}
System.IO.File.WriteAllText(@"___OUT___", sb.ToString());
return "wrote " + sb.Length + " chars";
