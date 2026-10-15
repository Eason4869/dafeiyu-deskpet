"""Select approved complete 480x520 RGBA cells; never scale or synthesize runtime art."""
import argparse
import copy
import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
FIRST_EIGHT = list(range(8))
INDICES = {
    "idle": FIRST_EIGHT,
    "running-right": [0, 1, 2, 3, 4, 5, 23, 7],
    "running-left": FIRST_EIGHT,
    "waving": [0, 1, 3, 5, 7, 12, 17, 19],
    "jumping": [0, 3, 5, 8, 11, 14, 18, 22],
    "failed": [0, 3, 5, 8, 11, 14, 16, 18],
    "waiting": FIRST_EIGHT,
    "running": [0, 2, 4, 7, 11, 17, 20, 23],
    "review": [0, 1, 3, 7, 10, 15, 18, 23],
    "lifted": FIRST_EIGHT,
    "landing": [0, 1, 3, 5, 8, 9, 12, 14],
    "petting": FIRST_EIGHT,
    "poked": [0, 1, 3, 5, 8, 11, 18, 23],
    "feeding": FIRST_EIGHT,
    "happy": [0, 1, 2, 4, 6, 9, 17, 21],
    "sleeping": FIRST_EIGHT,
    "special": [0, 2, 4, 7, 9, 15, 19, 23],
}
DURATIONS = {
    "running-right": 1.76, "running-left": 1.76,
    "jumping": 1.0, "landing": 1.0, "poked": 1.0,
    "sleeping": 3.2, "special": 2.0,
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def contained(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError(f"Asset escapes bundle: {relative}")
    return path


def build(source, output, report_dir):
    source, output, report_dir = source.resolve(), output.resolve(), report_dir.resolve()
    if source == output or output.is_relative_to(source):
        raise ValueError("Output must be separate from the original source pack")
    manifest = json.loads((source / "pet.json").read_text(encoding="utf-8-sig"))
    if manifest["cell_width"] != 480 or manifest["cell_height"] != 520 or set(manifest["animations"]) != set(INDICES):
        raise ValueError("Expected the complete original 17-action 480x520 pack")
    original_hashes = {str(p.relative_to(source)): digest(p) for p in source.rglob("*") if p.is_file()}
    result = copy.deepcopy(manifest)
    output.mkdir(parents=True, exist_ok=True)
    report_dir.mkdir(parents=True, exist_ok=True)
    report = {"source": str(source), "output": str(output), "operation": "exact-full-cell-selection",
              "runtime_resampling": False, "runtime_blending": False, "actions": {}, "contacts": []}
    contacts = []
    for action, indices in INDICES.items():
        original = manifest["animations"][action]
        source_path = contained(source, original["file"])
        image = Image.open(source_path)
        if image.mode != "RGBA" or original["columns"] != 8 or original["frames"] != 24 or image.size != (3840, 1560):
            raise ValueError(f"Unexpected original image: {action} {image.mode} {image.size}")
        atlas = Image.new("RGBA", (3840, 520))
        qa = Image.new("RGB", (1920, 292), "#edf3f8")
        draw = ImageDraw.Draw(qa)
        records = []
        for position, index in enumerate(indices):
            tile = image.crop((index % 8 * 480, index // 8 * 520, (index % 8 + 1) * 480, (index // 8 + 1) * 520))
            alpha = tile.getchannel("A")
            bounds = alpha.getbbox()
            if bounds is None or bounds[0] <= 0 or bounds[1] <= 0 or bounds[2] >= 480 or bounds[3] >= 520:
                raise ValueError(f"Empty or boundary-clipped frame: {action} source {index}, bounds={bounds}")
            # No mask argument: preserve every RGBA channel, including hidden RGB bytes.
            atlas.paste(tile, (position * 480, 0))
            records.append({"output_index": position, "source_index": index, "alpha_bbox": list(bounds),
                            "rgba_sha256": hashlib.sha256(tile.tobytes()).hexdigest()})
            # Reduced copies exist only in work/ QA contacts, never runtime assets.
            thumb = tile.resize((240, 260), Image.Resampling.LANCZOS)
            qa.paste(thumb, (position * 240, 30), thumb)
            draw.text((position * 240 + 8, 8), f"{action} | {position} <- {index}", fill="#172d48")
        target = contained(output, original["file"])
        target.parent.mkdir(parents=True, exist_ok=True)
        atlas.save(target, optimize=True)
        with Image.open(target) as decoded:
            if decoded.mode != "RGBA" or decoded.size != (3840, 520):
                raise AssertionError(f"Encoded output mode/size changed: {action}")
            for position, record in enumerate(records):
                pixels = decoded.crop((position * 480, 0, (position + 1) * 480, 520)).tobytes()
                if hashlib.sha256(pixels).hexdigest() != record["rgba_sha256"]:
                    raise AssertionError(f"Pixel mismatch after PNG encode: {action}/{position}")
        duration = DURATIONS.get(action, 1.6)
        result["animations"][action].update(columns=8, frames=8, fps=8 / duration)
        contact = report_dir / f"contact-{action}.png"
        qa.save(contact)
        contacts.append(qa)
        report["actions"][action] = {"source_sha256": digest(source_path), "output_sha256": digest(target),
                                     "source_indices": indices, "duration_seconds": duration, "fps": 8 / duration,
                                     "loop_unchanged": result["animations"][action]["loop"] == original["loop"],
                                     "all_pixels_exact": True, "frames": records, "contact": str(contact)}
    for relative in {manifest["preview"], manifest["gaze"]["file"]}:
        target = contained(output, relative)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(contained(source, relative), target)
        if digest(target) != digest(contained(source, relative)):
            raise AssertionError(f"Unchanged resource mismatch: {relative}")
    (output / "pet.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for key in manifest:
        if key != "animations" and result[key] != manifest[key]:
            raise AssertionError(f"Metadata changed: {key}")
    for group in range(0, len(contacts), 4):
        sheet = Image.new("RGB", (1920, 292 * min(4, len(contacts) - group)), "#edf3f8")
        for offset, qa in enumerate(contacts[group:group + 4]):
            sheet.paste(qa, (0, offset * 292))
        target = report_dir / f"contact-group-{group // 4 + 1:02}.png"
        sheet.save(target)
        report["contacts"].append(str(target))
    after = {str(p.relative_to(source)): digest(p) for p in source.rglob("*") if p.is_file()}
    if original_hashes != after:
        raise AssertionError("Original source files changed")
    report.update(source_unchanged=True, metadata_unchanged=True, preview_sha256=digest(output / manifest["preview"]),
                  gaze_sha256=digest(output / manifest["gaze"]["file"]), frame_count=136,
                  all_pixels_exact=True, alpha_boundaries_clear=True,
                  visual_review="Parent selected source indices; output contacts await full-body visual review")
    report_path = report_dir / "xiaobei-low8-validation.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "frames": 136, "actions": 17, "pixel_equality": True,
                      "source_unchanged": True, "report": str(report_path), "contacts": report["contacts"]}, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT.parent / "xiaobei-pet-v2")
    parser.add_argument("--output", type=Path, default=ROOT / "assets/bundled-skins/xiaobei")
    parser.add_argument("--report-dir", type=Path, default=ROOT / "work/xiaobei-low8-qa")
    args = parser.parse_args()
    build(args.source, args.output, args.report_dir)
