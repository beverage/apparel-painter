# Media

Assets for the README and the Workshop page. Kept out of the shipped mod:
release packaging copies an allowlist, never this folder — and publishing
never goes through the dev symlink (the in-game uploader ships the mod
folder verbatim: `media/`, `Source/`, `.git` and all).

## Production state

CAMPAIGN COMPLETE (2026-08-30): the page copy
(`steam-description.bbcode`) is validator-clean with zero TODO markers
(`devtools/bbcode-preview.py`, which also hard-fails past Steam's 8,000
plain-character limit), and every canonical asset is committed. Capture
ended up BRIDGE-DRIVEN, reversing the note that used to sit here: the
2026-08-25 abandonment was itself reversed once typed input landed
(2026-08-29), and the finals were staged, framed and shot through
Andreas Pardeike's RimBridgeServer automation bridge, scripted by the
drivers in `devtools/bridge/`, with a locally extended build supplying
the typed input for the picker card. Each recipe below names the driver
that produced its cut.

The pipeline:

1. `devtools/run-scene.sh --media --bridge --alongside` — isolated
   instance, Media build (SCENES stages compiled in, no ECR in the
   frame), bridge on 127.0.0.1:5175 with the token printed at launch.
2. Dev mode → Debug actions → Apparel Painter → build a stage, then
   click its south-west corner cell:
   - **Gif stage** (22×12): the busy full-surface set — six undyed
     stands, two Armor Racks, sbz hanger + display shelves, Jane in the
     teal duster (dropper source) and John, plus two carpet rugs
     (burgundy, forest green) for the dropper's floor beat.
   - **Wardrobe stage** (96×64): the clean stills set — eight
     formal-wear stands (Royalty), Synthread and alpaca stuffs,
     noon-pinned lighting, a dyed reference pair south of the row, and
     a second action ("Paint wardrobe stage", same corner cell) that
     commits the throne-room palette through the picker's own accept
     path. A before/after pair is two captures with one debug action
     between them.
3. Drive the shot with its `devtools/bridge/` script (stepped,
   readback-verified captures; OBS against the display only for
   continuous beats). Masters land in `~/Movies`.
4. Cut with shift-change's `devtools/footage.sh`, or `devtools/make-ab.sh`
   for A/B crossfades. Every treatment (gif ramps, scene cards, UI cards,
   Preview stills) is documented in `shift-change/media/README.md`;
   `RimWordFont.ttf` lives at `~/Downloads/RimWordFont.ttf`.

**Record every cut's parameters in this file at the time.** The sibling
lost a gif's recipe by skipping this and paid a re-derivation.

## Shot list (historical: the plan as filed, every row since shot; the recipes below are canonical)

| # | Asset | Fills | How |
|---|---|---|---|
| 1 | Core-loop gif | "What it does" gif TODO | Wardrobe wall: open Paint tab, paint one garment through the picker (live map preview), Accept, then Paint all on the neighbour |
| 2 | Dropper gif | "The picker" gif TODO | Gif stage: picker open, map dropper sips Jane's duster → apply; re-arms, sips the burgundy rug → apply |
| 3 | Picker card | "The picker" card TODO | Native-res capture on the flat `#1f242a` field, sibling UI-card treatment (crop to the window border, `-filter point` if upscaling); saved-swatch band populated, tab dropper overlays in shot |
| 4 | FAQ card | "FAQ" card TODO | No capture — text card; the questions are already listed in the TODO |
| 5 | `About/Preview.png` | Workshop upload (required; missing) | 640×360 crop of the hero treatment; re-set the title at crop scale, never scale it |
| 6 | Gallery cards | Workshop screenshots | Wardrobe before/after pair (same crop, both moments); where-it-works composite: stand + rack + sbz shelf off the gif stage |
| 7 | Integrations card + flip | Workshop screenshots (secondary) | Integrations scene: sbz hanger shelf with 8 rainbow dusters + LWM clothing rack (light Core fill, one teal shirt); selection hops hanger → rack, Paint tab follows. Needs LWM's Deep Storage (continued build 3532608331; packageId unchanged `LWM.DeepStorage`) |
| 8 | Core-loop reshoot | Replaces #1's cut | Minimal two-stand scene in the dropper's screen zone (the wall take's near-full-frame crop halved the UI); same eleven beats, dropper-style tight crop |

Page plumbing: EVERYTHING embeds via raw.githubusercontent — gifs
included (decided 2026-08-29: every cut is 108-690 KB, Steam's [img]
animates any https gif — the ~1 MB cap is a GALLERY-upload limit, not
a description limit — and the repo must be public for the banners
regardless, so imgur added an upload step plus its deletion-policy
risk for nothing). Consequence: the page renders only once media/ is
committed, pushed, and the repo is public — the same gate the banners
always had. Re-run `devtools/bbcode-preview.py` after every edit.

GALLERY AND PREVIEWFILE ARE TWO MECHANISMS (2026-08-28, refined
2026-08-30): `previewfile` — what the uploader ships from
About/Preview.png — is the SIDEBAR image on the item page and the
browse-grid thumbnail. The gallery ("additional previews") is added
through the item page's owner controls after upload, a manual
publish-tail step; gallery slots keep gif format and play when
selected (NL FA's slots serve `image/gif`, 905 KB, under the ~1 MB
per-file cap). A page with NO gallery promotes the previewfile into
the big area, which is what makes the two easy to conflate.
**The previewfile itself can ANIMATE**: the 08-28 reading called NL
FA's previewfile a static card, but its shipped About/Preview.png is
GIF89a bytes under the .png name (verified 2026-08-30 by magic bytes;
full mechanism in gamedata/workshop-publishing.md) — nothing validates
bytes against extension, and clients content-sniff and animate,
including in the browse grid. For this mod (principal, 2026-08-30):
About/Preview.png IS the animated flip's GIF bytes (242 KB); the
static titled card lives at media/preview-static.png and is the
natural gallery-slot-1 candidate.

## Recipes

### interest-hero.png

2026-08-24, the sibling fade+title treatment applied to an in-game
capture. Its exact crop and invocation were not recorded — it predates
this file, and is the reason this file exists.

### cards/ — the section banners

Minted by `banner-export.sh`: shift-change's banner template
(`shift-change/media/cards/_export/Banner_WhatItDoes.html` — a live
dependency on the sibling's tooling), retitled per section, rendered by
headless Chrome at 2×, trimmed and flattened onto `#1f242a`.
`cards/_export/` holds the regenerated HTML and is gitignored.

### wardrobe-row-dusk.gif and wardrobe-pair-dusk.gif (2026-08-28)

The dusk A/B flips, shot through the bridge. Instance:
`devtools/run-scene.sh --media --bridge --alongside` (bridge forced to
127.0.0.1:5175); shoot:

```
devtools/bridge/shoot-ab.py 5175 apparelpainter-scene-bridge --dusk
```

Stage at the driver's default origin (77,93) — map centre, which is what
keeps edge fog out of frame — dusk = the "Pin lighting: dusk" action at
DuskHour 19.0. Masters and cuts live in
`~/Movies/apparel-painter-dusk-2026-08-28/`: row 586×154, pair 1436×1026
(`paddingCells` pulls a third stand into the "pair" frame — man, woman,
man — same as the salvage take). Cut with `make-ab.sh` defaults
(HOLD 16 / BLEND 8 / DELAY 6 → 52 frames):

```
devtools/make-ab.sh before-row__cell_rect.png after-row__cell_rect.png wardrobe-row-dusk.gif
devtools/make-ab.sh before-pair__cell_rect.png after-pair__cell_rect.png wardrobe-pair-dusk.gif
```

Take 2 (`take2-stresemann/`, same day): after the Stresemann wardrobe
change — VAE suit jacket in black over the dark grey waistcoat — and the
TRUE two-shot pair rect (width 3; width 5 plus padding had pulled a third
stand in). The animated Workshop preview candidate is the pair cropped to
16:9 and resized BEFORE cutting, so the gif is born preview-sized:

```
magick before-pair__cell_rect.png -gravity center -crop 1026x577+0+0 +repage -resize 640x360 preview-before.png
magick after-pair__cell_rect.png  -gravity center -crop 1026x577+0+0 +repage -resize 640x360 preview-after.png
devtools/make-ab.sh preview-before.png preview-after.png preview-pair-dusk.gif   # 640x360, 52 frames, 226 KB
```

DUSK VARIES PER RELAUNCH: quicktest rolls a random world each launch, so
a pinned 19.0h lands at a different sun position every time — take 1 came
out amber, take 2 cold blue-grey. The clock pin is exact; the planet is
not. Judge the cast per take and re-shoot, or dial
`ApparelPainter.DuskHour` (TweakValues) between runs.

### preview-animated.gif and About/Preview.png (2026-08-28, blessed; reframed same day)

The titled two-stand flip is the Workshop page's ANIMATED preview —
uploaded at publish as SteamCMD's `previewfile`, since the in-game
uploader only sends the static `About/Preview.png` (which is the gif's
after-frame under the same overlay). `media/wardrobe-row-dusk.gif` is the
description headliner; its imgur upload is pending and a TODO marker
holds its slot in the bbcode.

The frame is the principal's 5×4-tile window (`take3-reframe/`,
`preview54-*`): 5 tiles across on whole edges (x first−1..first+4, the
pair centred, no third stand), 4 tiles tall with HALF-TILE trims top and
bottom (z stand−0.5..stand+3.5) — stands anchored low, the sky carrying
the title, 640×512 (5:4; Steam does not take square previews). Cut from
the driver's generous 9×8-cell pair capture at 205 px/cell:

```
magick before-pair__cell_rect.png -crop 1026x820+410+308 +repage preview54-before-native.png
magick after-pair__cell_rect.png  -crop 1026x820+410+308 +repage preview54-after-native.png
```

**HORIZONTAL EDGE RULE, refined twice by eye: never let a frame edge
land in MetalTile's panel BORDER zone.** The panel texture carries a
border and rounded corners at each tile's perimeter; an edge cutting
through that zone renders a "missing floor" void band (the rejected 16:9
centre crop and 6×5 half-offset window both hit it — the 6×5 also let a
third stand's half-body into shot). An edge on the grid line is clean,
and so is an edge at the panel MIDLINE (this frame's 0.5 trims): pure
interior surface reads as floor running off-frame. Only the in-between
cuts are ugly.

**SHIP NATIVE, QUANTIZE WITH FFMPEG** (same day, after a pixelation
complaint on the 640-wide cut): no resize at all — the browser
downscales better than we do and retina viewers get full detail — and
ImageMagick's gif quantizer speckles the dusk gradients where ffmpeg's
`palettegen stats_mode=diff` + bayer stays clean (the footage.sh
lesson, reapplied). Steam's preview cap is ~1 MB; this ships at 387 KB
(the lossless `-O3` variant lands at 691 KB if lossy=60 ever offends).
Title is the sibling treatment scaled to native (77% frame width):

```
magick -background none -fill "#f5f0e7" -font ~/Downloads/RimWordFont.ttf \
       -pointsize 70 label:"APPAREL PAINTER" -trim +repage title.png     # 789x65
magick -size 1026x820 xc:none \( -size 1026x205 gradient:black-none \) -composite \
       title.png -gravity north -geometry +0+29 -composite overlay.png
magick preview54-before-native.png overlay.png -composite bt.png
magick preview54-after-native.png  overlay.png -composite at.png
magick \( bt.png -duplicate 16 \) \( bt.png at.png -morph 8 -delete 0 \) \
       \( at.png -duplicate 16 \) \( at.png bt.png -morph 8 -delete 0 \) \
       +adjoin frames/f-%02d.png
ffmpeg -framerate 50/3 -i frames/f-%02d.png \
       -vf palettegen=max_colors=256:stats_mode=diff palette.png
ffmpeg -framerate 50/3 -i frames/f-%02d.png -i palette.png \
       -lavfi "paletteuse=dither=bayer:diff_mode=rectangle" -loop 0 raw.gif
gifsicle -O3 --lossy=60 raw.gif -o preview-animated.gif                  # 1026x820, 387 KB
magick at.png -strip About/Preview.png                                   # the static, same frame
```

### core-loop.gif (2026-08-28)

The description's "What it does" process demo: eleven stepped beats on
the wardrobe wall at noon — select stand 1, Paint tab, vest picker,
scarlet live on the stand, Accept; stand 2, Paint all, sapphire, Accept,
finale — shot headless by `devtools/bridge/shoot-core-loop.py`
(composition, colours, and the bridge traps it paid for are documented
in the driver). Dwell lives in ffconcat durations, so each beat is ONE
gif frame with its own delay:

```
devtools/bridge/shoot-core-loop.py 5175 apparelpainter-scene-bridge
# beat masters + beats.ffconcat archived in
# ~/Movies/apparel-painter-core-loop-2026-08-28/ (take5/ = the
# whole-stand-scarlet miss, kept for comparison)
ffmpeg -f concat -safe 0 -i beats.ffconcat -vf "crop=2790:1740:0:105,\
scale=1395:870:flags=lanczos,palettegen=max_colors=256:stats_mode=diff" palette.png
ffmpeg -f concat -safe 0 -i beats.ffconcat -i palette.png -lavfi "crop=2790:1740:0:105,\
scale=1395:870:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:diff_mode=rectangle" \
  -fps_mode vfr -loop 0 core-loop-raw.gif
gifsicle -O3 --lossy=60 core-loop-raw.gif -o core-loop.gif   # 12 frames, 1395x870, 600 KB
```

The crop starts at y=105 because the Dev palette window shrugs off
close_window and squats top-left with the dev icon row; the architect
bar and the alert rail fall off the bottom and right edges the same way.

### core-loop.gif (2026-08-29, reshot on the minimal scene — supersedes the wall take)

Principal: the wall take's near-full-frame crop halved every UI
element. Same eleven beats, same driver, new set: the scene parks two
formal stands (`DebugTools_CoreLoopScene`, dressed through the
wardrobe stage's own builders) in the dropper's screen zone — cluster
left of the picker, above the tab — and the frame crops dropper-style,
so the UI lands at 75% of retina-native instead of 50%. Masters in
`~/Movies/apparel-painter-core-loop-2026-08-29/take1/`; the wall
take's masters remain in `.../2026-08-28/`.

```
devtools/bridge/shoot-core-loop.py 5175 apparelpainter-scene-bridge
ffmpeg -y -f concat -safe 0 -i beats.ffconcat -vf "crop=1840:1352:0:502,\
scale=1380:1014:flags=lanczos,palettegen=max_colors=256:stats_mode=diff" palette.png
ffmpeg -y -f concat -safe 0 -i beats.ffconcat -i palette.png -lavfi "crop=1840:1352:0:502,\
scale=1380:1014:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:diff_mode=rectangle" \
  -fps_mode vfr -loop 0 core-loop-raw.gif
gifsicle -O3 --lossy=60 core-loop-raw.gif -o core-loop.gif   # 12 frames, 1380x1014, 635 KB
```

The crop's y=502 clears the dev message lines while keeping headroom
over the hats; the bottom edge (1854) sits flush on the menu bar so
the gizmo row stays whole. x stops at 1840 — 30 px past the picker's
right edge — which drops the alert rail the same way the dropper cut
does.

### core-loop.gif (2026-08-29 v3 — paint-all-then-rescue, parked picker)

Recoloured and repacked on the principal's direction, same day: the
men's stand builds PRE-SUITED (dress white / waistcoat grey / black
tie) and stands as context; the women's stand builds undyed. Paint
all floods her outfit scarlet off the saved band — shirt included,
which is what bulk painting honestly does — and the per-item beat
then rescues the shirt back to dress white. Ten beats; paint-all,
per-item precision, and the saved swatches in one arc. End state:
red dress, white shirt, black suit beside them.

Two new levers this cut leans on: the SCENES action **"Park picker
window here"** (execute_debug_action with a cell — the open picker's
top-left lands at that cell's screen position; silent on success so
no message line photobombs the frame), and the driver REBUILDING THE
SAVED BAND in setup — run-scene.sh wipes scenedata on every launch,
and the band lives in that instance's ModSettings.

```
devtools/bridge/shoot-core-loop.py 5175 apparelpainter-scene-bridge   # ~12 min; silent until exit
ffmpeg -y -f concat -safe 0 -i beats.ffconcat -vf "crop=1728:1340:0:514,\
scale=1296:1005:flags=lanczos,palettegen=max_colors=256:stats_mode=diff" palette.png
ffmpeg -y -f concat -safe 0 -i beats.ffconcat -i palette.png -lavfi "crop=1728:1340:0:514,\
scale=1296:1005:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:diff_mode=rectangle" \
  -fps_mode vfr -loop 0 core-loop-raw.gif
gifsicle -O3 --lossy=60 core-loop-raw.gif -o core-loop.gif   # 11 frames, 1296x1005, 563 KB
```

Masters in `~/Movies/apparel-painter-core-loop-2026-08-29/take2-v3/`.
The park cell (origin+6, origin+5) drops the picker into what used to
be dead centre, right of the stands; the crop hugs its right edge
(x=1728), y=514 keeps top-hat headroom, bottom flush on the menu bar.

v4, the same evening (principal: paint HIS suit on camera too; her
sequence locked as shot in v3): both stands build undyed again, and
act one suits him piece by piece off the band — shirt white, vest
grey, jacket black, top hat black, one compressed picker cycle per
garment with only the first showing its picker-open beat — before her
act runs verbatim. Seventeen beats, same crop and scale; 18 frames,
1296x1005, 693 KB. Masters in `.../take3-v4/`.

v5 (principal: make the clicks visible, subtly): the driver now dumps
a CLICK MANIFEST — each interaction records the control's rect, the
following beat adopts it — and assembly composites a 32px
native-style pointer, tip resting at ~(62%,66%) of the control so its
colour stays readable, body hanging off. Eleven marked frames (tab
buttons, row swatches, Paint all, every band adopt); nothing marked
where the engine already signals (selection brackets, closed dialogs).
Tooling: `devtools/bridge/composite-clicks.py` +
`devtools/bridge/stage-cursor-32.png` (the vanilla cursor is
bundle-packed, so the sprite is drawn — white fill, dark outline,
hotspot 3,1); invocation:
`composite-clicks.py <take-dir> <driver-output-file> <cursor-png>`,
run on working copies, never on raw/. FIELD FACT the pass surfaced: RimWorld
renders UI 1:1 ON THE RETINA BUFFER — layout rects are master-pixel
coordinates (band cells x1120+28n, y1217, w26 = SwatchCell's own 26)
— so composite math needs NO 2x scaling, and it is also why unscaled
UI reads small on this display. 18 frames, 1296x1005, 690 KB.
Masters in `.../take4-v5/` (raw/ = unmarked).

### dropper.gif (2026-08-28, reshot same day on the minimal scene)

The picker section's dropper demo: nine stepped beats — duster picker,
map dropper armed, Jane's worn teal sipped through the categorised
float menu with hex labels, the burgundy rug sipped instantly off the
bare floor, Accept — with every element ADJACENT on screen: the scene
(`DebugTools_DropperScene`: ONE dressed stand, Jane, one rug) parks its
world cluster left of the picker and above the tab at zoom 12, filling
what earlier takes wasted as empty steel, and the frame crops to a
third of the first cut. Shot by `devtools/bridge/shoot-dropper.py`
(which builds the wardrobe stage first purely for its ambient steel
field); assembly as core-loop.gif but `crop=1840:1325:0:520` →
1380×994. 10 frames, 504 KB. Masters in
`~/Movies/apparel-painter-dropper-2026-08-28/take-minimal/`, with the
superseded universal-stage cut beside them.

PER-SCENE MINIMAL STAGING is the rule this asset set now follows
(principal, 2026-08-28): a scene stages exactly what its shot needs,
positioned against the fixed UI panels for a tight crop; vanilla
pieces only in main shots; storage scenes must configure storage that
actually holds its fill. The universal gif stage is retired for
footage.

Readability pass (2026-08-29, three principal catches): Jane spawns
facing SOUTH (her north-facing back read as another stand); the game's
click-feedback ring is TICK-aged and froze under Jane through later
beats (step ~40 ticks after the menu pick to expire it — it spawns
only on the MENU path, never for direct floor sips); and because a
bare-floor sip draws no engine marker at all, the sip-rug beat gets
the mod's own Dropper.png composited at the sampled cell — restoring
the cursor attachment a real player sees and captures lose:

```
magick drop-06-sip-rug.png \( Textures/ApparelPainter/UI/Dropper.png -resize 84x84 \) \
  -geometry +1105+1275 -composite drop-06-sip-rug.png   # before assembly
```

Five takes, and the reusable lesson is in the driver's comments: a
destroyed pawn or container DROPS everything it wore or held, so a
Destroy-based sweep must sweep its own droppings (3×3 around each
corpse cell), reshoots at a shifted stage origin orphan the set pieces
outside the new pad's ClearArea, and idle model pawns from earlier
takes keep LIVING between shoots — walks, swims, wardrobe changes —
until destroyed.

### dropper.gif (2026-08-29 v2 — pinned hover; the parked-ring postmortem)

The principal spotted a soft pink ring parked in the cut's lower
right across the armed beats and asked if it marked our right-click.
Postmortem: it was the ARMED TARGETER'S CELL HIGHLIGHT rendering at
the HARDWARE mouse position — the engine highlights whatever cell
the perceived mouse hovers while a targeter is armed, our injected
sips never move that mouse, and beat()'s clear_hover_target actively
unmasks the real cursor (parked lower-right since that morning's
click-forward). v2 turns the artifact into the indicator: every
armed beat pins `set_hover_target` on the sip cell, so the engine
draws its ring ON Jane for the menu beat and ON the rug for the
floor sip — and the dropper mouse-attachment glyph rides the pinned
hover too, natively rendering the cursor the old cut had to
hand-composite (the Dropper.png magick step above is RETIRED). Same
composition, beats and crop as the blessed cut; the click manifest
adds the subtle pointer on the duster row swatch and the arm click
(devtools/bridge/composite-clicks.py, now prefix-agnostic).

```
devtools/bridge/shoot-dropper.py 5175 apparelpainter-scene-bridge
devtools/bridge/composite-clicks.py take-hover/ <driver-output> devtools/bridge/stage-cursor-32.png
ffmpeg -y -f concat -safe 0 -i beats.ffconcat -vf "crop=1840:1325:0:520,\
scale=1380:994:flags=lanczos,palettegen=max_colors=256:stats_mode=diff" palette.png
ffmpeg -y -f concat -safe 0 -i beats.ffconcat -i palette.png -lavfi "crop=1840:1325:0:520,\
scale=1380:994:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:diff_mode=rectangle" \
  -fps_mode vfr -loop 0 dropper-raw.gif
gifsicle -O3 --lossy=60 dropper-raw.gif -o dropper.gif   # 10 frames, 1380x994, 574 KB
```

Menu dwell RETIMED same evening (principal: the menu no longer read):
with the ring active through the whole armed stretch, the 1.4s menu
beat perceptually dissolved into the ring action — the frame was
present and correct (verified by coalescing the shipped gif), it just
read as a blink. beats.ffconcat holds menu-on-jane at 2.4s now; dwell
lives in assembly, so the fix cost no reshoot.

### The jacketless sweep closes the campaign (2026-08-29, late)

The VAE suit jacket audit (principal: hero shots must not depend on
third-party apparel): DebugTools_WardrobeStage and the core-loop
scene dropped `VAE_Apparel_SuitJacket`; men now commit dress-white
shirt + BLACK-TIE VEST and top hat — with no jacket over it, the
vest IS the suit read. Core-loop v6 reshot sixteen beats
(shirt-white / vest-black / hat-black act one), cut at
1600x1156+0+252 native off the 2560x1440 canvas — NOTE: the instance
follows whichever display it opens on, so masters are 2560x1440 or
3024x1890 per boot and crops are derived per take from the manifest,
never reused. The dusk A/B reshot at 156.25 px/cell; the 5x4 preview
window in TILE terms is unchanged, in pixels on this take:

```
devtools/bridge/shoot-ab.py 5175 apparelpainter-scene-bridge --dusk
magick before-pair__cell_rect.png -crop 781x625+312+234 +repage preview54-before.png
magick after-pair__cell_rect.png  -crop 781x625+312+234 +repage preview54-after.png
magick -background none -fill "#f5f0e7" -font ~/Downloads/RimWordFont.ttf \
       -pointsize 53 label:"APPAREL PAINTER" -trim +repage title.png    # 595px = 77% of 781
magick -size 781x625 xc:none \( -size 781x156 gradient:black-none \) -composite \
       title.png -gravity north -geometry +0+22 -composite overlay.png
# bt/at composites, 16+8+16+8 morph frames, palettegen diff + bayer at
# 50/3, gifsicle -O3 --lossy=60 — the 08-28 preview recipe unchanged.
devtools/make-ab.sh before-row__cell_rect.png after-row__cell_rect.png wardrobe-row-dusk.gif
```

Principal accepted this roll's darker cast with the full set
assembled. SUPERSEDES the 08-28 preview/row cuts; the gallery pair
finally landed as the untitled window crops
(media/gallery-wardrobe-before/after.png). Masters in
`~/Movies/apparel-painter-dusk-2026-08-29/take1-jacketless/`.

RE-FRAMED HALF A TILE UP (2026-08-30, principal: the pair sat too
close to the bottom edge): same masters, the 5×4 window drops half a
tile in screen space — `-crop 781x625+312+312` (y 234 → 312) — so the
stands gain a full clean tile below their bases and both horizontal
edges now land ON grid lines (the edge rule's other legal cut; the
old frame used panel midlines). Overlay reused as-is; morph chain,
palettegen diff + bayer and gifsicle lossy 60 identical; outputs land
at the same budgets (flip 242 KB, static 308 KB) and About/Preview.png
keeps its GIF89a bytes. Intermediates in
`take1-jacketless/recenter-halfup/`; the 08-29 framing's candidates
stay beside the masters for lineage. The GALLERY PAIR followed the
same day (principal): media/gallery-wardrobe-before/after.png are the
untitled crops of the NEW window, so every Workshop surface built on
the pair scene shares one framing.

### WIDENED TO SEVEN TILES, TYPE RAISED TO THE SIBLING'S BANNER SCALE (2026-08-30)

Principal, looking at the live Workshop page: fill the width, seven tiles, and
raise the title to Shift Change's banner size. Both came out of the SAME
17:45 masters — no reshoot, because a 7-tile window still fits the 9-cell
pair capture.

```
magick before-pair-1745__cell_rect.png -crop 1093x625+312+312 +repage pv-before.png
magick after-pair-1745__cell_rect.png  -crop 1093x625+312+312 +repage pv-after.png
magick -background none -fill "#f5f0e7" -font ~/Downloads/RimWordFont.ttf \
       -pointsize 75 label:"APPAREL PAINTER" -trim +repage title.png    # 846x70
magick -size 1093x625 xc:none \( -size 1093x156 gradient:black-none \) -composite \
       title.png -gravity north -geometry +0+22 -composite overlay.png
# frames, ffconcat durations, palettegen diff + bayer, gifsicle -O3 --lossy=60
#   -> 1093x625, 19 frames, 410 KB (Steam's item-image cap is ~1 MB)
```

**The offsets did not move — only the width.** `+312+312` is unchanged, so the
edge rule still holds: the window spans cells `first-1 .. first+5`, both
vertical edges on GRID LINES, both horizontal edges the half-tile panel
midlines of the 08-30 reframe.

**A third stand is now in frame, and it is symmetric, not an accident.**
Stands sit every 2 cells from `first`, so seven tiles cannot hold the pair
centred without reaching `first+4`. At this offset the three land on tiles
2, 4 and 6 of 7 — man, woman, man, with the centre stand dead centre and an
empty tile at each edge. The old 5x4 rule ("no third stand") was a rule for a
5-tile window; at 7 the same centring argument produces three.

**Type: match pointsize-to-frame-WIDTH, never the ink box.** Shift Change's
preview title measures 389x37 at +121+13 on 640x360 — pointsize 44, gradient
90 (25% of height), y 13. Scaled to our 1093: pointsize `44/640*1093 = 75`,
gradient `0.25*625 = 156`, y `13/640*1093 = 22`. The gradient and y offset
ALREADY matched; only the type was short. Note the old 53 was also on the
sibling's scale (`44/640*781 = 53.7`) — widening the frame without touching
the type is what would have shrunk it, which is what the principal saw.

Do NOT derive the pointsize from Shift Change's ink-box RATIO (389/640 = 61%
of frame width). That number is a property of the STRING: "SHIFT CHANGE" is
twelve characters, "APPAREL PAINTER" is fifteen, so ours lands at 846/1093 =
77% at the same type size. Chasing 61% would have set pointsize ~59 and made
the title SMALLER than the 5-tile version it was meant to grow past.

The GALLERY PAIR followed, as the 08-30 entry above requires — every Workshop
surface built on the pair scene shares one framing, so
`gallery-wardrobe-before/after.png` are the untitled 1093x625 crops of this
window off the same 17:45 masters. They had still been the old dark take.

### THE THIRD STAND TAKES A COMBAT KIT (2026-08-30, reshot at 17:00)

Principal, on the widened frame: with room for a third stand, kit it out —
flak armour and a devilstrand duster — and paint it as a coherent field rig.
The row now reads formal / formal / FIELD instead of three variations on a
suit, which is a better argument for a mod that paints anything worn.

Five garments, five separate commits on one stand:

| garment | def | colour | hex |
|---|---|---|---|
| button-down shirt | `Apparel_CollarShirt` | Tan 499 | `#C3B091` |
| flak pants | `Apparel_FlakPants` | Ranger Green | `#6B705C` |
| flak vest | `Apparel_FlakVest` | Coyote Brown | `#81613C` |
| flak helmet | `Apparel_AdvancedHelmet` | Coyote Brown | `#81613C` |
| duster | `Apparel_Duster` (Devilstrand) | Olive Drab | `#5A6237` |

Green uniform, brown armour: coyote has been the plate-carrier and
helmet-cover standard since roughly 2010, so cover and carrier match, and tan
499 is the shirt worn under them. All clear the BlackTie luminance floor.

**THE FLAK HELMET'S defName IS `Apparel_AdvancedHelmet`.** Label and defName
disagree. A defName search for `Apparel_FlakHelmet` returns nothing and reads
as "vanilla has no flak helmet", which is false. SEARCH APPAREL BY `<label>`,
not by the name you expect: the same check finds `Apparel_CollarShirt` is the
button-down, and that vanilla has no tunic at all.

Two pieces genuinely take no stuff: flak vest and flak pants have no
`stuffCategories`, their cloth being a fixed `costList` ingredient rather than
a material choice. The stuff argument passed for them is inert — `Garment`
skips the lookup when `MadeFromStuff` is false. The duster (Fabric) and shirt
(Fabric) are real material choices; the helmet is Metallic-only, hence steel.

**Layering, measured rather than predicted.** The duster is `Shell` over the
vest's `Middle` and the pants' `OnSkin` and covers Torso/Neck/Shoulders/Arms/
Legs, so the kit was expected to collapse into "a green coat". It does not —
the duster's worn graphic has an OPEN FRONT, so the tan shirt reads clearly at
the chest. The weak piece is the VEST: coyote brown multiplied over an already
dark texture leaves it a shadow under the collar. If the armour ever needs to
read, lighten the vest or drop the duster; do not darken anything.

**THE CAPTURE SCALE CHANGED WITH THE DISPLAY.** This boot returned 1846x1640
pair rects (205.1 px/cell) where the 08-30 morning take returned 1406x1250
(156.25) — the instance follows whichever display it opens on, exactly as the
core-loop entry warns. The window is still 7x4 tiles two cells in on both
axes, so every number is re-derived, never reused:

```
devtools/bridge/sweep-dusk.py 5175 apparelpainter-scene-bridge --from=12 --to=21
devtools/bridge/shoot-ab-hour.py 5175 apparelpainter-scene-bridge --hour=17
devtools/make-ab.sh before-row-1700__cell_rect.png after-row-1700__cell_rect.png \
                    wardrobe-row-dusk.gif                    # 586x154, 52 frames, 135 KB
magick before-pair-1700__cell_rect.png -crop 1436x820+410+410 +repage pv-before.png
magick after-pair-1700__cell_rect.png  -crop 1436x820+410+410 +repage pv-after.png
magick -background none -fill "#f5f0e7" -font ~/Downloads/RimWordFont.ttf \
       -pointsize 99 label:"APPAREL PAINTER" -trim +repage title.png   # 1114x91 = 77.6%
magick -size 1436x820 xc:none \( -size 1436x205 gradient:black-none \) -composite \
       title.png -gravity north -geometry +0+29 -composite overlay.png
# 19 frames, ffconcat durations, palettegen diff + bayer, gifsicle -O3 --lossy=60
#   -> 1436x820, 716 KB (Steam item-image cap ~1 MB)
```

Type is still Shift Change's banner scale — `44/640 * 1436 = 99`, gradient 25%
of height, y `13/640 * 1436 = 29`. The ratios travel; the pixel numbers do not.

**THE HOUR HAS NOW COST THREE DIFFERENT NUMBERS FOR ONE LOOK.** Same target
(meanY ~28.8, warmth ~+10), three world rolls: 17:45, then 15:54, then 17:00
flat. Never carry an hour between launches — sweep, match on the numbers,
shoot. This roll happened to land on a whole hour and needed no fine pass.

Masters: `~/Movies/apparel-painter-dusk-2026-08-30/` (see the note there; the
scene folder is wiped by `run-scene.sh` at every launch).

### cards/card-faq.png (2026-08-29)

Text card, no capture: the SIBLING's card-faq export page with our
Q&A entries swapped into its entries block — same CSS, same
embedded RimWord face, same `.q` run-in idiom — rendered by the
banner-export Chrome recipe (2x, transparent, trim, flatten onto
#1f242a). The page is kept at `media/cards/_export/card-faq.html`;
regenerating is re-running the Chrome + trim commands from
banner-export.sh against it.

The dye-cost question is deliberately absent (principal, 08-29): the
drafted "why is painting free?" answer leaned on a DLC-gating story
that our own record contradicts — Dye and its recipes are CORE — and
a strings scan of DubsPaintShop.dll settled the rest:
`JobDriver_PaintCells`/`_PaintThings` carry `CollectDyeToils` /
`FindNearbyDyes`, so DUBS RUNS THE DYE ECONOMY WITHOUT IDEOLOGY.
Raising the cost question in our FAQ would only spotlight the
comparison; it is not answered because it is not raised.

The FOURTH question is ordering (principal, 08-30): tab order vs
shelf order. Fact-checked before shipping: the "sbz orders by
quality" hypothesis did NOT verify — ASF's only quality logic is the
BUILDING's CompQuality scaling capacity (maxItemsPerCellByQuality in
ThingClass.cs); nothing sorts displayed items by quality. What
players actually see is OUR tab's canonical sort (name → quality →
condition, BL-079) against the container's own draw order (arrival,
for vanilla and ASF alike). The card says exactly that and tells
them to match garments by swatch, not position.

WITH THIS CARD THE DESCRIPTION IS COMPLETE: zero TODO markers,
validator-clean at 4,061 plain characters.

Masters in `~/Movies/apparel-painter-dropper-2026-08-29/take-hover/`
(raw/ = unmarked). Pipeline note that saves the next session a scare:
the FIRST driver after a cold boot sits ~5-6 quiet minutes in visual
readiness while FasterGameLoading's background pass settles — warm
reruns start in seconds. Not a hang.

### where-it-works.gif and cards/card-where-it-works.png (2026-08-28)

The where-it-works gallery pieces, VANILLA ONLY per the staging rule:
`DebugTools_StorageScene` stages one dressed stand, one Core wood shelf
holding exactly three garments (teal + burgundy dusters in the set's
palette, natural shirt — 3 of 6 slots, spawned into the shelf's own
OccupiedRect because multi-cell buildings place by centre), and one
small shelf with a bowler. `devtools/bridge/shoot-where-it-works.py`
hops the selection stand → shelf → small shelf; the Paint tab follows.
The still is the shelf beat at native crop; the flip is all four beats
(idle 1.0s, 1.8s per container):

Layout revised same day (principal): vanilla storage is exactly these
two shelves, so the row folds into an L — the small shelf two tiles
below the 2x1, flush with its right edge (derived from the shelf's own
OccupiedRect) — and the canvas narrows from 1650 to 1080 wide
(`take-L/`):

```
magick wiw-02-shelf.png -crop 1080x1105+0+740 +repage cards/card-where-it-works.png  # 516 KB
ffmpeg -f concat -safe 0 -i beats.ffconcat -vf "crop=1080:1105:0:740,\
palettegen=max_colors=256:stats_mode=diff" palette.png
ffmpeg -f concat -safe 0 -i beats.ffconcat -i palette.png \
  -lavfi "crop=1080:1105:0:740[x];[x][1:v]paletteuse=dither=bayer:diff_mode=rectangle" \
  -fps_mode vfr -loop 0 wiw-raw.gif
gifsicle -O3 --lossy=60 wiw-raw.gif -o where-it-works.gif             # 1080x1105, 402 KB
```

Masters in `~/Movies/apparel-painter-where-it-works-2026-08-28/`. The
first take left the burgundy duster on the floor beside the shelf —
the tab honestly refused to list it, proving the apparel-present gate,
but improper storage is exactly what this scene must not show.

### integrations.gif and cards/card-integrations.png (2026-08-29)

The modded-storage gallery pieces (shot-list row 7):
`DebugTools_IntegrationsScene` stages one sbz hanger shelf carrying
its advertised 8 — dusters painted through a full rainbow, spawned
four-then-four in ascending x so the sweep reads left to right — and
one LWM clothing rack with a legal light fill (three shirts and a
bowler under LWM's 2.5 kg/cell cap, one shirt in the set's teal).
`devtools/bridge/shoot-integrations.py` hops the selection
hanger → rack over three beats; the card is the hanger beat at native
crop:

```
devtools/bridge/shoot-integrations.py 5175 apparelpainter-scene-bridge
magick int-01-hanger.png -crop 1300x1234+0+620 +repage cards/card-integrations.png  # 795 KB
ffmpeg -y -f concat -safe 0 -i beats.ffconcat -vf "crop=1300:1234:0:620,\
palettegen=max_colors=256:stats_mode=diff" palette.png
ffmpeg -y -f concat -safe 0 -i beats.ffconcat -i palette.png \
  -lavfi "crop=1300:1234:0:620[x];[x][1:v]paletteuse=dither=bayer:diff_mode=rectangle" \
  -fps_mode vfr -loop 0 integrations-raw.gif
gifsicle -O3 --lossy=60 integrations-raw.gif -o integrations.gif    # 4 frames, 1300x1234, 439 KB
```

Masters in `~/Movies/apparel-painter-integrations-2026-08-29/`
(take1 = the west-edge clip — a 2-wide piece with drawSize-4 hanging
art needs the camera a cell closer than a 1-wide stand; take2 = the
loose-pile miss, caught by the principal on the delivered candidate:
a freshly spawned sbz/LWM piece ACCEPTS NOTHING — empty
defaultStorageSettings — and storage renderers arrange only accepted
stock, so an unconfigured fill draws as a centre-of-cell heap instead
of the neat hanging row. Scenes now tick the fill's defs via
`DebugTools_GifStage.Allow` before spawning; take3 fixed the hanger
only — the principal asked after the LWM rack's memberships, and its
Allow call had indeed been missed. take4 = both containers configured,
the keeper; the rack renders identically either way — LWM's display
draws cell contents regardless of acceptance — so the recut is for
provenance, not pixels).
TAKE 6 (2026-08-30) is the keeper: the ARMOR RACK joined the row
(principal — the description lists it, the shot didn't show it;
armor set duster + flak jacket + flak helmet via IThingHolder, no
storage settings on that family), and the shoot doubled as the
DEFERRED RACK-FAMILY TAB-ORDER VERIFICATION: its contents tab is
labelled "Rack" and the strip reads Rack | Paint | Storage — correct
as-is, no further match-widening needed. Take 5 (fogged) forced the
root fix: the wardrobe stage's builder now calls
map.fogGrid.ClearAllFog() — reveal used to spread from wherever the
quicktest colonists spawned, and an unlucky world dimmed half the
frame. Take 6 put the rack THIRD IN LINE facing east — side profile,
dead lower-middle gap; TAKE 7 is the keeper (principal): the armor
rack folds into the where-it-works L — two below the row, flush with
the LWM rack's right edge, FACING SOUTH so the flak set reads
front-on. Keeper cut on the 2560 canvas: card
`magick int-01-hanger.png -crop 1120x938+0+470`, flip same crop over
four beats (idle 1.0 + 3×2.0), palettegen diff + bayer, gifsicle
lossy 60. Masters take7-L/ (take5-fogged/, take6-armor/ kept for
lineage).

Field notes: LWM's DEFAULT settings convert other
mods' storage — every sbz piece and the vanilla shelves — into Deep
Storage units at init, and the shot is honest about the result: the
hanger reports LWM's "8 / 8 stacks" in the inspect pane while ASF's
item rendering carries on untouched (LWM's hiding patches never reach
ASF's own renderer). Tab order holds Contents | Paint | Storage on
both families because the startup's insert now also matches LWM's
"Inventory"-named contents tab (ApparelPainterStartup, 2026-08-29).

### cards/card-the-picker.png (2026-08-29)

The picker UI card (shot-list row 3), shot through the BRIDGE FORK's
typed input on the core-loop scene: `devtools/bridge/shoot-picker-card.py`
opens the picker on stand 1's formal shirt, then saves eight swatches —
the throne-room palette plus the set's teal — each typed into the hex
field at 10 cps (controlName `ApparelPainter_DirectInput`), applied via
the Set button and verified by RGB readback, saved via the ~20px plus
cell and verified by diffing the band's button_invisible cells. The
last colour (teal) stays live on the shirt's collar in the master.

```
devtools/bridge/shoot-picker-card.py 5175 apparelpainter-scene-bridge   # ~10 min; silent until exit
magick pick-01-card-master.png -crop 591x521+1213+692 +repage \
  -bordercolor "#1f242a" -border 48 cards/card-the-picker.png           # 687x617, body-only
magick pick-01-card-master.png -crop 1840x1314+0+540 +repage card-picker-wide.png  # alternate: picker + tab + stands
```

Masters in `~/Movies/apparel-painter-picker-card-2026-08-29/take1/`
(card-master, picker-open, and the wide alternate). The crop is
BODY-ONLY, measured not eyeballed (principal caught the first cut's
two flaws: the drag bar's lower half with its grip icon, and the crop's
left column sitting ON the window's light edge line). The precise-crop
technique: dump a 1px `magick … txt:-` column through the suspect band
— the bar fill (#212529), its separator row, and the body fill
(#15191D) read straight off the enumeration, and the same trick finds
edge-line columns. Cheaper than iterating cuts by eye. Un-annotated per
the 08-27 shot plan; the bbcode TODO's "annotated" idea remains an
option layered on this master. The bbcode slot now embeds the card
(validator-clean, 0 problems).

### RESHOT WARMER AND BRIGHTER (2026-08-30) — and the hour is a CURVE, not a setting

Principal: the animated preview and the eight-stand row read dark next to
every other gif on the page. Measured, they did: mean luminance 22% and 23%
against 28-30% for core-loop, dropper, integrations and where-it-works. All
four of those were shot at the noon pin; the two dark ones are the only
assets re-aimed at dusk.

**The real mechanism, and it retires "DUSK VARIES PER RELAUNCH" as a thing
you just live with.** Sun glow is
`GenCelestial.CelestialSunGlowPercent(latitude, dayOfYear, dayPercent)`, so a
pinned hour lands somewhere different on every world roll. That framing led
to judging the cast BETWEEN takes, which never converged, because each take
was a different planet. Sweep hours WITHIN one roll instead and the whole
curve is comparable. On the 08-30 roll:

| hour | meanY | R,G,B | warmth R-B |
|---|---|---|---|
| 12-17 | 31.0% | 79,79,78 | +1 (pixel-identical across the band) |
| 17:45 | 28.8% | 77,72,67 | +10 |
| 18 | 27.0% | 75,67,58 | +17 |
| 19 | 20.3% | 65,49,32 | +33 |
| 20 | 18.3% | 40,48,52 | -12 (blue night) |

The take that shipped measured R44 G45 B46, warmth -2, 17.9% — which on this
curve sits between 19h and 20h. So `DuskHour 19.0` was pinned to the EDGE of
a one-hour amber window: 08-28 take 1 landed inside it (warmth +11), the
shipped take fell just past it into the blue. Nothing was random about it.

**Warm is not free, and past 18h it costs the mod its subject.** At 18h and
19h the amber wash drags the GARMENTS: the dress-white shirt goes cream, the
pale-blue vests go olive-khaki. For a mod whose whole claim is "this is the
colour you painted", a cast that lies about the after-colour works against
the asset — which is what `PinLighting`'s own comment means by noon being
"right for stills that must show true fabric colour". 17:45 is the corner of
the curve: the warmth of the take everyone liked (+10 vs its +11) at the
brightness of the noon-shot siblings (28.8% vs their 28-30%), with the
garments still true. Shipped at 30% (flip) and 32% (row).

**THE HOUR IS NOT PORTABLE.** 17:45 is not a setting to reuse — it is where
this roll's curve turned. Re-sweep every shoot; the driver exists so that is
cheap.

Tooling added for it (all SCENES-only, none of it ships):

- `Pin lighting: +1 hour` debug action — re-pins the clock an hour on and
  re-clamps the weather, so a sweep stays comparable step to step.
- `devtools/bridge/sweep-dusk.py` — builds the stage, walks noon to 21h,
  captures pair + row at each hour.
- `devtools/bridge/shoot-ab-hour.py` — shoot-ab.py at an ARBITRARY hour.
  `--hour` walks with the debug action; `--minutes` finishes in raw ticks
  (2500/hour), because a finer debug action would need a rebuild, a rebuild
  needs a relaunch, and a relaunch re-rolls the world the hour was chosen on.

The cut, on the 08-30 half-tile-up framing (`+312+312`) unchanged:

```
devtools/bridge/sweep-dusk.py 5175 apparelpainter-scene-bridge --from=12 --to=21
devtools/bridge/shoot-ab-hour.py 5175 apparelpainter-scene-bridge --hour=17 --minutes=45
devtools/make-ab.sh before-row-1745__cell_rect.png after-row-1745__cell_rect.png \
                    wardrobe-row-dusk.gif                      # 446x118, 52 frames, 69 KB
magick before-pair-1745__cell_rect.png -crop 781x625+312+312 +repage pv-before.png
magick after-pair-1745__cell_rect.png  -crop 781x625+312+312 +repage pv-after.png
# overlay exactly as 08-29: pointsize 53 title centred +0+22, 781x156 gradient
# 19 frames: bt, morph(bt->at,8,-delete 0), morph(at->bt,8,-delete 0)
# durations in an ffconcat (1.02 / 0.06 / 1.08 / 0.06), palettegen diff + bayer,
# gifsicle -O3 --lossy=60                                       # 781x625, 284 KB
```

**Correction to the entries above: the shipped flip is NOT a 16+8+16+8 morph
chain.** The artifact on disk is 19 frames with delays 102 / 6x8 / 108 / 6x9 —
the holds are ONE long-delay frame each, not sixteen duplicates, which is the
whole reason it fits in ~250 KB rather than ~390 KB. Whoever re-cut it that
way did not record it, and the recipe here still described the 52-frame
version. Dwell therefore belongs in ffconcat durations (the core-loop idiom),
not in `-duplicate`; ffmpeg keeps the quantizing either way.

Also: `gifsicle -b out.gif --delay N '#0'` does NOT edit one frame's delay in
place. Frame selectors SUBSET — that call rewrites the file down to frame 0
alone. There is no in-place per-frame delay edit here; set dwell at assembly.

Masters: `~/Movies/apparel-painter-dusk-2026-08-30/` — the four
`before|after-{pair,row}-1745` cell rects, with the hour curve's own frames
under `sweep/`. NOT the scene folder: `run-scene.sh` opens with
`rm -rf "$SCENEDATA"`, so anything left in `dist/scenedata/Screenshots` dies
at the next scene launch. Scene-instance captures are ephemeral by
construction — copy out whatever a shipped cut depends on, the same as every
other entry above does.

### (recorded at cut time)

One subsection per shot-list asset — the exact `footage.sh` and `magick`
invocations, masters, crops and ramps — added as each is produced.
