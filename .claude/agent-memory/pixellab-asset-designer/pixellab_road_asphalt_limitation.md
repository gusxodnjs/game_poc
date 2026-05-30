---
name: pixellab-road-asphalt-limitation
description: create_topdown_tileset can't make clean smooth-asphalt-with-lane-line road; pushes to brick/cobble. Use guidance ~9 + selective outline + basic shading for the least-textured flat gray.
metadata:
  type: feedback
---

PixelLab `create_topdown_tileset` resists "smooth flat gray asphalt with dashed lane line" for car-road upper terrain — it keeps rendering a brick/cobble/stone pattern even with explicit negatives ("no bricks, no cobblestones, no stones, no pattern").

**Why:** corner-Wang tiles are corner-defined per 32px cell, so a continuous centered lane marking can't be placed reliably, and the model's prior for "paved road" leans masonry. Pushing harder (lineless + flat shading + text_guidance_scale 14) made it WORSE — produced an explicit purple-brick grid (#52 retune, tileset 2e0d6b35, discarded).

**How to apply:** For a flat gray road, prefer the milder settings that gave the best result in #52: `outline=selective outline`, `shading=basic shading`, `detail=low detail`, `text_guidance_scale=9`, prompt "smooth flat gray asphalt road surface, faint dashed white lane line down the center, even uniform paving, no bricks, no cobblestones". That yields a flat-ish gray mottled surface (tileset 6c5b98bf, kept) — distinct from cobble and from grass, but WITHOUT a crisp dashed lane line (PixelLab won't paint it). If a real lane line is mandatory, post-process it in PIL after assembly rather than expecting PixelLab to render it. Forest (darker canopy vs light grass) came out great at the same mild settings. See [[pixellab-topdown-tileset-corner-wang]] and [[pixellab-uniform-terrain-quirk]].
