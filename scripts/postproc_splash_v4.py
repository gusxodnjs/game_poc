#!/usr/bin/env python3
"""Postprocess splash v4 frames: download from PixelLab URL, apply
corner-color alpha clipping (memory: pixellab-no-background-quirk),
write RGBA PNG to disk + corresponding .meta file with fresh GUID.

Library functions are importable; CLI processes a single frame.

Usage (CLI):
    postproc_splash_v4.py <url> <output_path>
    postproc_splash_v4.py --file <input_path> <output_path>

The corner-color alpha clipping handles PixelLab's no_background:true
quirk where solid background is sometimes returned despite the flag.
"""
from __future__ import annotations

import argparse
import sys
import urllib.request
import uuid
from collections import Counter
from io import BytesIO
from pathlib import Path

from PIL import Image

EXPECTED_SIZE = (256, 256)
ALPHA_CLIP_TOL = 12
CORNER_ALPHA_MAX = 32

# Template for Unity TextureImporter .meta of pixel-art PNG.
# Mirrors the exact pattern of existing splash v3 PNG .meta files in the
# repo (e.g. Assets/AppIcon/splash_v3_planet_160.png.meta and
# Assets/AppIcon/splash_anim_v2_bigbang_256_f00.png.meta) — including
# serializedVersion 13, all platformSettings (Default/Standalone/iOS),
# bumpmap.flipGreenChannel, swizzle, spriteSheet.customData /
# spriteCustomMetadata, root-level mipmapLimitGroupName / pSDRemoveMatte.
# Single source of truth: re-running the script does NOT overwrite an
# existing .meta (preserves GUID).
META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: iOS
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

PNG_MAGIC = b"\x89PNG\r\n\x1a\n"


def download_png(url: str, timeout: int = 60) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "splash-v4-postproc/1.0"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        data = resp.read()
    if not data.startswith(PNG_MAGIC):
        raise RuntimeError(f"not a PNG (head={data[:16]!r})")
    return data


def alpha_clip_background(raw_png: bytes, tol: int = ALPHA_CLIP_TOL) -> bytes:
    img = Image.open(BytesIO(raw_png)).convert("RGBA")
    w, h = img.size
    corners = [
        img.getpixel((0, 0))[:3],
        img.getpixel((w - 1, 0))[:3],
        img.getpixel((0, h - 1))[:3],
        img.getpixel((w - 1, h - 1))[:3],
    ]
    br, bg_, bb = Counter(corners).most_common(1)[0][0]
    new_pixels = []
    for r, g, b, a in img.getdata():
        if a == 0 or abs(r - br) + abs(g - bg_) + abs(b - bb) <= tol:
            new_pixels.append((r, g, b, 0))
        else:
            new_pixels.append((r, g, b, a))
    img.putdata(new_pixels)
    buf = BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def write_meta(png_path: Path) -> Path:
    """Write Unity TextureImporter .meta with fresh GUID. Idempotent — if
    .meta already exists, do NOT overwrite (preserve existing GUID)."""
    meta_path = png_path.with_suffix(png_path.suffix + ".meta")
    if meta_path.exists():
        return meta_path
    meta_path.write_text(META_TEMPLATE.format(guid=uuid.uuid4().hex))
    return meta_path


def process_to_file(raw_png: bytes, out_path: Path) -> dict:
    """Apply alpha clip, validate, write PNG + .meta. Returns info dict."""
    img_check = Image.open(BytesIO(raw_png))
    if img_check.size != EXPECTED_SIZE:
        raise RuntimeError(f"unexpected size {img_check.size}, expected {EXPECTED_SIZE}")

    clipped = alpha_clip_background(raw_png)
    if not clipped.startswith(PNG_MAGIC):
        raise RuntimeError("clip output not PNG")

    img = Image.open(BytesIO(clipped))
    w, h = img.size
    corners = [
        img.getpixel((0, 0))[3],
        img.getpixel((w - 1, 0))[3],
        img.getpixel((0, h - 1))[3],
        img.getpixel((w - 1, h - 1))[3],
    ]
    max_corner = max(corners)
    if max_corner > CORNER_ALPHA_MAX:
        raise RuntimeError(
            f"alpha clip insufficient: max corner alpha={max_corner} > {CORNER_ALPHA_MAX}"
        )

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(clipped)
    meta_path = write_meta(out_path)
    return {
        "path": str(out_path),
        "meta_path": str(meta_path),
        "size_bytes": len(clipped),
        "corner_alphas": corners,
    }


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", help="URL or path (with --file)")
    parser.add_argument("output", help="Output PNG path")
    parser.add_argument("--file", action="store_true", help="Read source as file path")
    args = parser.parse_args(argv)

    if args.file:
        raw = Path(args.source).read_bytes()
    else:
        raw = download_png(args.source)

    info = process_to_file(raw, Path(args.output))
    print(
        f"OK {info['path']} ({info['size_bytes']}B, "
        f"corners={info['corner_alphas']}, meta={info['meta_path']})"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
