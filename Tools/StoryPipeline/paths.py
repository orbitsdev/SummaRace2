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


SCRIPTS = os.path.join(PROJECT, "Assets", "_Game", "Scripts")
RACE_DIRECTOR = os.path.join(
    SCRIPTS, "Features", "Race", "Endless", "EndlessRaceDirector.cs")


def story_paths():
    import glob
    return sorted(glob.glob(os.path.join(STORIES, "s*.json")))


def race_director_source():
    """The shipping race's source, for tools that must read runtime geometry rather than
    restate it. Returns '' if the file has moved, so a caller can fail loudly."""
    try:
        with open(RACE_DIRECTOR, encoding="utf-8") as fh:
            return fh.read()
    except OSError:
        return ""
