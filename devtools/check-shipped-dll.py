#!/usr/bin/env python3
"""Assert the committed assembly is a shippable Release build.

    ./devtools/check-shipped-dll.py [path/to/ApparelPainter.dll]

Ported from shift-change's gate of the same name (2026-09-02, at this mod's
second release). Two properties, pulling in opposite directions:

  1. NO SCENES fixtures. The stage builders clear multi-hundred-cell
     footprints without confirmation and leave player-faction buildings and
     colonists behind. A Debug or Media dll committed by mistake puts that
     in a player's debug menu, one unconfirmed click away.

  2. The harness DOES ship. `-apparelpainter-harness` is the release gate,
     and the gate is only worth running if it asserts against the literal
     assembly players install. Over-gating would delete the gate silently,
     leaving a green run that asserted nothing.

This mod is unusually exposed to (1): `run-scene.sh --media` writes the SAME
`Assemblies/ApparelPainter.dll` the release ships, so any filming session
leaves a SCENES build on disk. The 2026-09-02 session rebuilt Release by
hand each time and got away with it; this script is what makes that a check
rather than a habit.

--------------------------------------------------------------------------
THE TRAP THIS SCRIPT EXISTS TO AVOID

A .NET assembly keeps names in THREE places with DIFFERENT encodings:

    #Strings  type and member names       UTF-8
    #US       string literals in code      UTF-16
    #Blob     attribute ARGUMENTS          UTF-8, length-prefixed

So `grep -a DebugTools_GifStage` works (a type name, UTF-8) while
`grep -a apparelpainter-harness` finds NOTHING whatever ships. Measured on
this mod's own Release dll, 2026-09-02:

    DebugTools_GifStage       utf8=0            (correctly compiled out)
    Harness                   utf8=3
    apparelpainter-harness    utf8=0  utf16=2   <-- the trap

A guard written on literals with an ASCII search therefore passes forever
and asserts nothing. Key every check on a TYPE NAME where possible; when a
literal is the only handle, search utf-16-le explicitly.
--------------------------------------------------------------------------
"""

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_DLL = os.path.join(ROOT, "Assemblies", "ApparelPainter.dll")

# Type names, so UTF-8. Every one of these lives in a file wrapped in
# #if SCENES and must compile out under Release.
FORBIDDEN_TYPES = [
    "DebugTools_CoreLoopScene",
    "DebugTools_DropperScene",
    "DebugTools_GifStage",
    "DebugTools_IntegrationsScene",
    "DebugTools_StorageScene",
    "DebugTools_WardrobeStage",
]

# The release gate, which ships in EVERY configuration by design: the body
# (Harness), its flag-gated boot (HarnessBoot) and the MonoBehaviour that
# waits for the map (HarnessDriver).
REQUIRED_TYPES = [
    "Harness",
    "HarnessBoot",
    "HarnessDriver",
]

# The feature surface. Not belt-and-braces: an over-eager #if or a dropped
# file would leave a dll that loads, shows a Paint tab, and silently has no
# style control — which is exactly the kind of absence nobody notices in a
# smoke test.
REQUIRED_TYPES += [
    "ITab_ApparelPainter",
    "ContainerAdapter",
    "ColorForcer",
    "StyleIndex",
    "StyleForcer",
]

# A literal, so UTF-16 — the one place we cannot key on a type.
REQUIRED_LITERALS = [
    "apparelpainter-harness",
]

failures = []


def fail(message):
    failures.append(message)


def utf8_count(blob, needle):
    return blob.count(needle.encode("utf-8"))


def utf16_count(blob, needle):
    return blob.count(needle.encode("utf-16-le"))


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_DLL
    if not os.path.exists(path):
        print("FAIL no assembly at %s" % path)
        return 1
    blob = open(path, "rb").read()

    for name in FORBIDDEN_TYPES:
        if utf8_count(blob, name):
            fail("%s is present — a SCENES build (Debug or Media) was "
                 "committed. run-scene.sh writes the same path; rebuild with "
                 "-c Release, which restores the shipping dll." % name)

    for name in REQUIRED_TYPES:
        if not utf8_count(blob, name):
            fail("%s is MISSING — something over-gated it behind #if SCENES, "
                 "or the file left the build." % name)

    for literal in REQUIRED_LITERALS:
        if not utf16_count(blob, literal):
            fail("the \"%s\" launch flag is MISSING (searched UTF-16, which "
                 "is where literals live) — run-harness.sh would launch the "
                 "game and never trigger a run." % literal)

    for failure in failures:
        print("FAIL %s" % failure)
    if failures:
        print("\n%d problem(s) in %s" % (len(failures), path))
        return 1
    print("shipped dll: no debug scenes, release gate intact, "
          "style control present")
    return 0


if __name__ == "__main__":
    sys.exit(main())
