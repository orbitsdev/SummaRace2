// Audit snippet: capture Screen Space Overlay canvases in edit mode.
// Run via mcp__UnityMCP__execute_code (CodeDom C#6). Substitute ___OUT___ with an
// absolute PNG path. AFTER running, the caller MUST reopen the scene from disk
// (EditorSceneManager.OpenScene without saving) to discard the dirty canvas state.
var camGo = new UnityEngine.GameObject("___AuditCam");
var cam = camGo.AddComponent<UnityEngine.Camera>();
camGo.transform.position = new UnityEngine.Vector3(9000, 9000, 9000);
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = UnityEngine.Color.black;
var canvases = UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None);
foreach (var cv in canvases)
{
    if (cv.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay)
    { cv.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; cv.worldCamera = cam; cv.planeDistance = 1f; }
}
var rt = new UnityEngine.RenderTexture(540, 960, 24);
cam.targetTexture = rt;
cam.Render();
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(540, 960, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 540, 960), 0, 0);
tex.Apply();
System.IO.File.WriteAllBytes(@"___OUT___", tex.EncodeToPNG());
UnityEngine.RenderTexture.active = null;
cam.targetTexture = null;
UnityEngine.Object.DestroyImmediate(camGo);
UnityEngine.Object.DestroyImmediate(rt);
UnityEngine.Object.DestroyImmediate(tex);
return "saved ___OUT___";
