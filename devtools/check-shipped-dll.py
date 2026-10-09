#!/usr/bin/env python3
"""Assert the committed assembly is a shippable Release build.

    ./devtools/check-shipped-dll.py [path/to/ApparelPainter.dll]

Ported from shift-change's gate of the same name (2026-09-02, at this mod's
second release). Three properties:

  1. NO SCENES fixtures. The stage builders clear multi-hundred-cell
     footprints without confirmation and leave player-faction buildings and
     colonists behind. A Debug or Media dll committed by mistake puts that
     in a player's debug menu, one unconfirmed click away.

  2. NO HARNESS, and no `-apparelpainter-harness` launch flag. Dev tooling is
     not a player's to carry. It is compiled out of Release entirely
     (ApparelPainter.csproj's HARNESS switch), so this asserts an ABSENCE.

     It used to be the opposite check — the harness was REQUIRED to ship, on
     the argument that a gate asserting against a build nobody installs
     asserts nothing. What replaced that argument: HARNESS is only ever a
     whole-file guard, so the harness build and the shipping build differ by
     the presence of Harness.cs's types and by nothing else.
     check-invariants.py enforces that half; this file enforces this one.

  3. The feature surface IS there. Absence checks pass trivially on an empty
     or truncated file, so something has to be asserted present.

This mod is unusually exposed to (1): `run-scene.sh --media` writes the SAME
`Assemblies/ApparelPainter.dll` the release ships, so any filming session
leaves a SCENES build on disk. The 2026-09-02 session rebuilt Release by
hand each time and got away with it; this script is what makes that a check
rather than a habit. run-harness.sh writes the same path and sweeps itself
afterwards, which is the same hazard answered the same way.

--------------------------------------------------------------------------
THE TRAP THIS SCRIPT EXISTS TO AVOID

A .NET assembly keeps names in THREE places with DIFFERENT encodings:

    #Strings  type and member names       UTF-8
    #US       string literals in code      UTF-16
    #Blob     attribute ARGUMENTS          UTF-8, length-prefixed

So `grep -a DebugTools_GifStage` works (a type name, UTF-8) while
`grep -a apparelpainter-harness` finds NOTHING whatever ships. Measured on
this mod's own dlls:

    DebugTools_GifStage       utf8=0            (correctly compiled out)
    apparelpainter-harness    utf8=0  utf16=2   <-- the trap, on the harness
                                                    build; 0/0 on shipping

A guard written on literals with an ASCII search therefore passes forever
and asserts nothing. Key every check on a TYPE NAME where possible; when a
literal is the only handle, search utf-16-le explicitly.
--------------------------------------------------------------------------
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_DLL = os.path.join(ROOT, "Assemblies", "ApparelPainter.dll")

# Type names, so UTF-8. The scene builders live in files wrapped in
# #if SCENES; the harness types in one wrapped in #if HARNESS. Both must
# compile out under a plain Release build.
FORBIDDEN_TYPES = [
    "DebugTools_CoreLoopScene",
    "DebugTools_DropperScene",
    "DebugTools_GifStage",
    "DebugTools_IntegrationsScene",
    "DebugTools_StorageScene",
    "DebugTools_WardrobeStage",
    "HarnessBoot",
    "HarnessDriver",
]

# A literal, so UTF-16 — the one place we cannot key on a type.
FORBIDDEN_LITERALS = [
    "apparelpainter-harness",
]

# The catch-all, and the only check here that covers code nobody has written
# yet: whatever a future harness file is called, it will contain this word.
# Measured on the shipping build, the word appears zero times in either
# encoding, so the bar is exact rather than a threshold.
#
# If a player-facing string ever legitimately contains "harness" — and this
# mod lists apparel, so that is not far-fetched — this fires, and the fix is
# to narrow THIS list, not to delete the check.
FORBIDDEN_TOKENS = [
    "harness",
]

# The feature surface. Not belt-and-braces: an over-eager #if or a dropped
# file would leave a dll that loads, shows a Paint tab, and silently has no
# style control — which is exactly the kind of absence nobody notices in a
# smoke test. It is also what keeps the absence checks above from passing
# on an empty file.
REQUIRED_TYPES = [
    "ITab_ApparelPainter",
    "ContainerAdapter",
    "ColorForcer",
    "StyleIndex",
    "StyleForcer",
]

failures = []


def fail(message):
    failures.append(message)


def utf8_count(blob, needle):
    return blob.count(needle.encode("utf-8"))


def utf16_count(blob, needle):
    return blob.count(needle.encode("utf-16-le"))


def token_hits(blob, token):
    """Every readable run containing the token, in both encodings.

    The UTF-16 pass strips the interleaved NULs first rather than searching
    for a decorated pattern, so a hit reports the same way whichever heap it
    came from.
    """
    pattern = re.compile(br"[\x20-\x7e]{0,40}" + re.escape(token.encode("utf-8"))
                         + br"[\x20-\x7e]{0,40}", re.IGNORECASE)
    hits = [match.decode("ascii", "replace") for match in pattern.findall(blob)]
    wide = blob.decode("utf-16-le", "ignore").encode("ascii", "ignore")
    hits += [match.decode("ascii", "replace") for match in pattern.findall(wide)]
    return hits


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_DLL
    if not os.path.exists(path):
        print("FAIL no assembly at %s" % path)
        return 1
    blob = open(path, "rb").read()

    for name in FORBIDDEN_TYPES:
        if utf8_count(blob, name):
            fail("%s is present — this is a SCENES build (Debug or Media) or "
                 "a -p:Harness=true one. run-scene.sh and run-harness.sh both "
                 "write this path; rebuild with plain -c Release, which "
                 "restores the shipping dll." % name)

    for literal in FORBIDDEN_LITERALS:
        if utf16_count(blob, literal):
            fail("the \"%s\" launch flag is present (found in UTF-16, which "
                 "is where literals live) — dev tooling does not ship. "
                 "Rebuild with plain -c Release." % literal)

    for token in FORBIDDEN_TOKENS:
        hits = token_hits(blob, token)
        if hits:
            fail("the word \"%s\" survives in the assembly (%d place(s), e.g. "
                 "%r) — something dev-only is not behind #if HARNESS."
                 % (token, len(hits), hits[0]))

    for name in REQUIRED_TYPES:
        if not utf8_count(blob, name):
            fail("%s is MISSING — something over-gated it, or the file left "
                 "the build." % name)

    for failure in failures:
        print("FAIL %s" % failure)
    if failures:
        print("\n%d problem(s) in %s" % (len(failures), path))
        return 1
    print("shipped dll: no debug scenes, no harness, "
          "feature surface and style control present")
    return 0


if __name__ == "__main__":
    sys.exit(main())
