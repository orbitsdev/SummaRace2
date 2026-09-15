Editor scripts used on 2026-09-15 to fix the runner art. Run inside the open Editor with the Unity CLI:

  unity command run_script --file Tools/RunnerPortraits/FixGirl.cs --entry FixGirl.Main
  unity command run_script --file Tools/RunnerPortraits/RenderRunners.cs --entry RenderRunners.FixNormals
  unity command run_script --file Tools/RunnerPortraits/RenderRunners.cs --entry RenderRunners.Render

Render writes Captures/runner_render_0/1.png; copy them over Assets/_Game/Resources/UI/Runners/runner_0/1.png (keep the .meta).
