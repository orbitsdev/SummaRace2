"""One place that knows where the project is.

extract/parse/emit/apply/flag each carried a hardcoded absolute path
(`C:\\Users\\User\\Documents\\2026\\GAME\\SummaRace2`) from the machine the pipeline was
first written on. That path does not exist on the owner's machine, so every one of those
scripts died on `FileNotFoundError` before doing anything -- i.e. the pipeline that owns
the 30 generated JSONs could not be run at all where the repo actually lives, and the
"flag.py reports 0" claim could not be reproduced.

Resolution order:
  1. $SUMMARACE_ROOT
  2. the repo this file is in (Tools/StoryPipeline/../..)
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def project_root():
    env = os.environ.get("SUMMARACE_ROOT")
    if env and os.path.isdir(env):
        return env
    root = os.path.abspath(os.path.join(HERE, "..", ".."))
    return root


PROJECT = project_root()
STORIES = os.path.join(PROJECT, "Assets", "_Game", "Resources", "Stories")
FONTS = os.path.join(PROJECT, "Assets", "Art", "Fonts", "TMP")
SCENES = os.path.join(PROJECT, "Assets", "_Game", "Scenes")


def story_paths():
    import glob
    return sorted(glob.glob(os.path.join(STORIES, "s*.json")))
