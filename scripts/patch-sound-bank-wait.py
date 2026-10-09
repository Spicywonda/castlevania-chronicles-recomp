#!/usr/bin/env python3
from pathlib import Path
import importlib.util
spec = importlib.util.spec_from_file_location("sprite_patch", Path(__file__).with_name("patch-sprite-visibility.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
path = Path(__file__).resolve().parents[1] / "generated/main.cs"
source = path.read_text()
result = module.transform(source, "func_8001624C", [
    ("c.A0 = 0x00000003u;", "c.A0 = Chronicles.SoundBankTransition.WaitMode(m);", 1),
])
if result != source:
    path.write_text(result)
print("Sound bank close: verified scoped gameplay wait")
