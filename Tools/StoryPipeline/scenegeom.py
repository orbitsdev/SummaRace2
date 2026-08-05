"""Resolve the on-screen rect of a TMP object in a scene, from the scene YAML.

The layouts these checks depend on live in .unity files, not in code, so hardcoding
"the option pill is 817x181" would go stale the first time somebody nudges an anchor.
This walks the RectTransform chain from the Canvas down and computes the real pixel size
at the CanvasScaler's reference resolution, which is what the autosize solver needs.

Read-only. Never writes a scene.
"""
import re

import paths
import os

_CACHE = {}


class Scene:
    def __init__(self, path):
        with open(path, encoding="utf-8", errors="replace") as f:
            text = f.read()
        parts = re.split(r"^--- !u!(\d+) &(-?\d+)", text, flags=re.M)
        self.docs = {}
        for i in range(1, len(parts), 3):
            self.docs[parts[i + 1]] = (parts[i], parts[i + 2])
        self.ref_res = (1080.0, 1920.0)
        for cls, body in self.docs.values():
            m = re.search(r"m_ReferenceResolution: \{x: ([\d.]+), y: ([\d.]+)\}", body)
            if m:
                self.ref_res = (float(m.group(1)), float(m.group(2)))
                break

    def body(self, fid):
        return self.docs[fid][1]

    def comps(self, go):
        return re.findall(r"component: \{fileID: (-?\d+)\}", self.body(go))

    def name(self, go):
        m = re.search(r"m_Name: (.*)", self.body(go))
        return m.group(1).strip() if m else "?"

    def find(self, name):
        out = []
        for fid, (cls, body) in self.docs.items():
            if cls == "1" and re.search(r"m_Name: %s\s*$" % re.escape(name), body, re.M):
                out.append(fid)
        return out

    def rect_component(self, go):
        for c in self.comps(go):
            if self.docs[c][0] == "224":
                return c
        return None

    def _vec(self, body, key):
        m = re.search(r"%s: \{x: ([-\d.eE+]+), y: ([-\d.eE+]+)\}" % key, body)
        return (float(m.group(1)), float(m.group(2))) if m else (0.0, 0.0)

    def size(self, go):
        """Pixel size at the canvas reference resolution."""
        chain = []
        cur = go
        while cur:
            rc = self.rect_component(cur)
            if rc is None:
                break
            b = self.body(rc)
            chain.append((self._vec(b, "m_AnchorMin"), self._vec(b, "m_AnchorMax"),
                          self._vec(b, "m_SizeDelta")))
            fa = re.search(r"m_Father: \{fileID: (-?\d+)\}", b).group(1)
            if fa == "0":
                break
            cur = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", self.body(fa)).group(1)
        w, h = self.ref_res
        for amin, amax, sd in reversed(chain[:-1] if len(chain) > 1 else chain):
            w = (amax[0] - amin[0]) * w + sd[0]
            h = (amax[1] - amin[1]) * h + sd[1]
        return w, h

    def tmp(self, go):
        """{fontAssetGuid, autosize, min, max, margin, wrapping} for a TMP on this object."""
        for c in self.comps(go):
            cls, b = self.docs[c]
            if cls != "114" or "m_fontSizeMin" not in b:
                continue
            def num(k, d=0.0):
                m = re.search(r"\b%s: (-?[\d.eE+]+)" % k, b)
                return float(m.group(1)) if m else d
            guid = re.search(r"m_fontAsset: \{fileID: \d+, guid: (\w+)", b)
            margin = re.search(
                r"m_margin: \{x: ([-\d.eE+]+), y: ([-\d.eE+]+), z: ([-\d.eE+]+), w: ([-\d.eE+]+)\}", b)
            wm = re.search(r"m_TextWrappingMode: (\d)", b)
            return dict(
                font_guid=guid.group(1) if guid else None,
                autosize=bool(num("m_enableAutoSizing")),
                fmin=num("m_fontSizeMin"), fmax=num("m_fontSizeMax"),
                fixed=num("m_fontSize"),
                margin=tuple(float(x) for x in margin.groups()) if margin else (0, 0, 0, 0),
                wrap=int(wm.group(1)) if wm else 1,
            )
        return None


def load(scene_name):
    if scene_name in _CACHE:
        return _CACHE[scene_name]
    s = Scene(os.path.join(paths.SCENES, scene_name + ".unity"))
    _CACHE[scene_name] = s
    return s
