#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def transform(source, function, edits):
    pattern = rf"(    public static void {function}\(.*?)(?=    public static void |\Z)"
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
    margin = "Chronicles.Widescreen.SpriteMargin"
    specs = [
        ("main.cs", "func_80022118", [
            ("c.A1 - 0x10u;", f"c.A1 - (0x10u + {actor});", 1),
            ("c.A1 + 0x120u;", f"c.A1 + (0x120u + {actor});", 2),
            ("Addi(2, 5, -16,", f"Addi(2, 5, (short)(-16 - (int){actor}),", 1),
            ("Addi(2, 5, 288,", f"Addi(2, 5, (short)(288 + (int){actor}),", 2),
        ]),
        ("stage1.cs", "func_80106804", [
            ("(int)c.V1 < -16 ?", f"(int)c.V1 < (-16 - {margin}) ?", 1),
            ("(int)c.V1 < 273 ?", f"(int)c.V1 < (273 + {margin}) ?", 2),
            ("Slti(2, 3, -16,", f"Slti(2, 3, (short)(-16 - {margin}),", 1),
            ("Slti(2, 3, 273,", f"Slti(2, 3, (short)(273 + {margin}),", 2),
        ]),
    ]
    pending = []
    for filename, function, edits in specs:
        path = ROOT / "generated" / filename
        original = path.read_text()
        result = transform(original, function, edits)
        if result != original:
            pending.append((path, result))
    for path, result in pending:
        path.write_text(result)
    print(f"Sprite visibility: verified 2 routines, updated {len(pending)} files")


if __name__ == "__main__":
    main()
