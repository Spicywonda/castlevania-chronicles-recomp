#!/usr/bin/env python3
from pathlib import Path
import importlib.util
spec = importlib.util.spec_from_file_location("sprite_patch", Path(__file__).with_name("patch-sprite-visibility.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
path = Path(__file__).resolve().parents[1] / "generated/main.cs"
source = path.read_text()
old = "if (c.V0 != 0u) {\n            goto L8001D7DC;"
new = "if (c.V0 != 0u || Chronicles.Cheats.IgnoreContact(m)) {\n            goto L8001D7DC;"
result = module.transform(source, "func_8001D458", [(old, new, 1)])
if result != source:
    path.write_text(result)
print("Contact invincibility: verified shared damage branch")
