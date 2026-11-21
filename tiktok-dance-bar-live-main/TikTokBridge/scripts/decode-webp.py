"""Decode static/animated WebP to the same 12 FPS PNG contract as GIF assets."""
import sys
from pathlib import Path
from PIL import Image

source, destination = Path(sys.argv[1]), Path(sys.argv[2])
with Image.open(source) as image:
    if image.format != "WEBP":
        raise ValueError("Expected WebP image")
    if image.width * image.height > 32000000:
        raise ValueError("Image exceeds 32 megapixels")
    count = 0
    elapsed = 0
    for index in range(getattr(image, "n_frames", 1)):
        image.seek(index)
        frame = image.convert("RGBA")
        duration = max(10, int(image.info.get("duration", 100)))
        frame.thumbnail((512, 512), Image.Resampling.LANCZOS)
        if not getattr(image, "is_animated", False):
            frame.save(destination / "frame_001.png")
            break
        end = elapsed + duration
        while count < 120 and count * 1000 / 12 < end:
            frame.save(destination / f"frame_{count + 1:03d}.png")
            count += 1
        elapsed = end
        if count >= 120:
            break
