# Autotile tileset layout (#52 지구 타일맵 리얼리티 개선, Task 1)

5 autotile sheets, each **128×128 px = 4×4 grid of 32×32 tiles**, top-down
GBA-Pokémon pixel style. Each feature blends into a bright lime-green grass
background (palette anchored to `grass_32.png`, base ≈ `#82cf1c`).

| Sheet | Feature (upper terrain) | Notes |
|-------|-------------------------|-------|
| `path_auto_128.png` | 흙길 — light brown packed earth (`#c8a86e`-ish) | soft soil border |
| `road_auto_128.png` | 포장 차도 — gray paved stone/asphalt | came out as gray cobble/brick texture, not smooth asphalt; still a distinct gray paved surface |
| `water_auto_128.png` | shallow blue water | sandy/wet shoreline transition into grass |
| `forest_auto_128.png` | dense dark-green canopy | scattered foliage at the grass edge |
| `building_auto_128.png` | building roof — gray tiled roof + wall edge | wall/foundation edge where it meets grass |

All 5 sheets share **ONE identical layout** (below). Index any sheet the same way.

---

## IMPORTANT: these are CORNER-Wang tiles, not 4-edge tiles

The task contract describes a **4-EDGE bitmask** (N=1, E=2, S=4, W=8 — a bit set
when the *same feature is the neighbor on that side*). PixelLab's
`create_topdown_tileset` produces **4-CORNER Wang tiles** instead: each tile is
defined by whether each of its 4 **corners** (NW/NE/SW/SE) is feature ("upper")
or grass ("lower").

These are two genuinely different autotiling schemes and **cannot be mapped 1:1**:
- Edge-mask tiles answer "does the feature extend past edge N/E/S/W?"
- Corner-Wang tiles answer "is corner NW/NE/SW/SE feature or grass?"

A corner set can render interiors, straight edges, outer corners AND inner
corners cleanly (16 distinct shapes). A 4-edge mask set cannot represent inner
corners at all (it has no bit for them). So the corner set is actually the
**richer, lossless** representation. The honest recommendation is therefore:

> **Drive the renderer from a 4-CORNER mask, not a 4-edge mask.**
> This is lossless and every one of the 16 cells is a meaningful, distinct tile.

The developer was told they can adapt the renderer's `MaskCell` table to whatever
consistent layout is delivered — this is that adaptation.

---

## Layout A (CANONICAL, lossless): corner index → cell

```
cornerIndex = NW*8 + NE*4 + SW*2 + SE*1     (NW/NE/SW/SE = 1 if feature, else 0)
col = cornerIndex % 4
row = cornerIndex / 4        (integer division; row 0 = TOP)
```

Grid (value in each cell = cornerIndex, i.e. NW NE / SW SE bits):

```
        col0        col1        col2        col3
row0 |  0  (····) |  1  (···SE)|  2  (··SW·)|  3  (··SWSE)|
row1 |  4  (·NE··)|  5  (·NE·SE)|  6 (·NESW·)|  7 (·NESWSE)|
row2 |  8  (NW···)|  9  (NW··SE)| 10 (NW·SW·)| 11 (NW·SWSE)|
row3 | 12  (NWNE··)| 13 (NWNE·SE)|14 (NWNESW·)|15 (NWNESWSE)|
```

- cell (0,0) = cornerIndex 0  = all-grass (verified 100% grass)
- cell (3,3) = cornerIndex 15 = solid feature interior (verified 0% grass)

### Godot `terrains_peering_bit` corner convention (if using Godot terrain set)
Each cell's 4 corners are exactly its bits:
`top_left=NW, top_right=NE, bottom_left=SW, bottom_right=SE`.

---

## Layout B (FALLBACK, lossy): 4-edge mask → cell

If the renderer must stay on the documented 4-edge bitmask (N=1,E=2,S=4,W=8),
use the table below. It maps each edge mask to the closest corner tile using the
rule *"a corner is feature iff BOTH of its adjacent sides are feature-covered"*.

**This is lossy.** Edge masks that touch only a single side or two opposite sides
(0,1,2,4,5,8,10) have no corner-tile equivalent and collapse to the all-grass
tile (cornerIndex 0) — i.e. those cells render as plain grass with no feature
nub. Acceptable ONLY if the map never produces those configurations; otherwise
prefer Layout A.

| edgeMask | sides (N E S W) | cornerIndex | cell (col,row) | faithful? |
|---------:|-----------------|------------:|----------------|-----------|
|  0 | ----    |  0 | (0,0) | n/a (isolated → grass) |
|  1 | N---    |  0 | (0,0) | NO (collapses to grass) |
|  2 | -E--    |  0 | (0,0) | NO |
|  3 | NE--    |  4 | (0,1) | yes (NE corner) |
|  4 | --S-    |  0 | (0,0) | NO |
|  5 | N-S-    |  0 | (0,0) | NO (opposite sides) |
|  6 | -ES-    |  1 | (1,0) | yes (SE corner) |
|  7 | NES-    |  5 | (1,1) | yes (NE+SE) |
|  8 | ---W    |  0 | (0,0) | NO |
|  9 | N--W    |  8 | (0,2) | yes (NW corner) |
| 10 | -E-W    |  0 | (0,0) | NO (opposite sides) |
| 11 | NE-W    | 12 | (0,3) | yes (NW+NE) |
| 12 | --SW    |  2 | (2,0) | yes (SW corner) |
| 13 | N-SW    | 10 | (2,2) | yes (NW+SW) |
| 14 | -ESW    |  3 | (3,0) | yes (SW+SE) |
| 15 | NESW    | 15 | (3,3) | yes (solid) |

---

## Generation provenance (reproducible)

- Tool: PixelLab MCP `create_topdown_tileset`, `tile_size=32`, `view=high top-down`,
  `outline=selective outline`, `shading=basic shading`, `detail=low detail`.
- `transition_size`: 0.25 for path/road/forest/building, 0.5 for water (wider shoreline).
- All 5 share the same grass lower base tile id `14da0cce-0d01-4186-8477-7517bfcdaa77`
  so the grass background is pixel-consistent across sheets.
- Tileset IDs: path `bcfbc054…`, road `231401df…`, water `ec95c120…`,
  forest `6ac349ba…`, building `e37f141b…`.
- Assembly: `scripts/assemble_autotiles.py` downloads each tileset's metadata+image,
  crops each tile by its `bounding_box`, and re-lays them in cornerIndex order
  (Layout A). Re-run that script to regenerate the sheets from the same IDs.
