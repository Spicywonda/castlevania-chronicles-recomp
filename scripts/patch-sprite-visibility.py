#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def transform(source, function, edits):
    pattern = rf"(    public static void {function}(?:_[A-Za-z0-9]+)?\(.*?)(?=    public static void |\Z)"
    matches = list(re.finditer(pattern, source, re.S))
    if len(matches) != 1:
        raise ValueError(f"Expected one {function}, got {len(matches)}")
    match = matches[0]
    body = match.group()
    for old, new, count in edits:
        if body.count(new) == count and old not in body:
            continue
        if body.count(old) != count or new in body:
            raise ValueError(f"Unexpected instruction in {function}: {old}")
        body = body.replace(old, new)
    return source[:match.start()] + body + source[match.end():]


def main():
    actor = "Chronicles.Widescreen.ActorMargin(m, c.S0)"
    margin = "Chronicles.Widescreen.SpriteMarginFor(m)"
    specs = [
        ("main.cs", "func_80022118", [
            ("c.A1 - 0x10u;", f"c.A1 - (0x10u + {actor});", 1),
            ("c.A1 + 0x120u;", f"c.A1 + (0x120u + {actor});", 2),
            ("Addi(2, 5, -16,", f"Addi(2, 5, (short)(-16 - (int){actor}),", 1),
            ("Addi(2, 5, 288,", f"Addi(2, 5, (short)(288 + (int){actor}),", 2),
        ]),
        ("stage1.cs", "func_80101268", [
            ("CastlevaniaChronicles_main.GsSortFastSprite(c, m);",
             "Chronicles.CloudRenderer.DrawCloud(c, m);", 2),
        ]),
        ("stage1.cs", "func_801065F8", [
            ("(int)c.V1 < -16 ?", f"(int)c.V1 < (-16 - {margin}) ?", 1),
            ("(int)c.V1 < 273 ?", f"(int)c.V1 < (273 + {margin}) ?", 2),
            ("Slti(2, 3, -16,", f"Slti(2, 3, (short)(-16 - {margin}),", 1),
            ("Slti(2, 3, 273,", f"Slti(2, 3, (short)(273 + {margin}),", 2),
        ]),
        ("stage1.cs", "func_80106804", [
            ("(int)c.V1 < -16 ?", f"(int)c.V1 < (-16 - {margin}) ?", 1),
            ("(int)c.V1 < 273 ?", f"(int)c.V1 < (273 + {margin}) ?", 2),
            ("Slti(2, 3, -16,", f"Slti(2, 3, (short)(-16 - {margin}),", 1),
            ("Slti(2, 3, 273,", f"Slti(2, 3, (short)(273 + {margin}),", 2),
        ]),
        ("stage1.cs", "func_80106E3C", [
            ("(int)c.V1 < -16 ?", f"(int)c.V1 < (-16 - {margin}) ?", 1),
            ("(int)c.V1 < 273 ?", f"(int)c.V1 < (273 + {margin}) ?", 2),
            ("Slti(2, 3, -16,", f"Slti(2, 3, (short)(-16 - {margin}),", 1),
            ("Slti(2, 3, 273,", f"Slti(2, 3, (short)(273 + {margin}),", 2),
        ]),
    ]
    pending = {}
    originals = {}
    for filename, function, edits in specs:
        path = ROOT / "generated" / filename
        if path not in originals:
            originals[path] = path.read_text()
        source = pending.get(path, originals[path])
        # Migrate previous generated patches before verifying their instructions.
        source = re.sub(r"Chronicles\.Widescreen\.SpriteMargin\b", margin, source)
        pending[path] = transform(source, function, edits)
    for filename, function in [("main.cs", "func_80022118"),
                               ("main.cs", "func_8001E0F0"),
                               ("stage1.cs", "func_80101268_stage1")]:
        path = ROOT / "generated" / filename
        source = pending[path]
        signature = f"    public static void {function}(CpuContext c, IMemory m)\n    {{"
        insertion = signature + "\n        using var wideViewport = Chronicles.WorldViewport.Begin(m);"
        if insertion not in source:
            if source.count(signature) != 1:
                raise ValueError(f"Missing world drawing scope: {function}")
            pending[path] = source.replace(signature, insertion)
    updated = 0
    for path, result in pending.items():
        if result != originals[path]:
            path.write_text(result)
            updated += 1
    print(f"Sprite visibility: verified {len(specs)} routines, updated {updated} files")



if __name__ == "__main__":
    main()
