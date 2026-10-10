#!/usr/bin/env python3
"""Scope the gallery presentation and interview input fixes to their original routines."""
from pathlib import Path
root = Path(__file__).resolve().parents[1]
def inject(file, old, new, marker):
    path = root / 'generated' / file
    source = path.read_text()
    if marker in source:
        if source.count(new) != 1:
            raise ValueError(f'Unexpected existing extras patch: {marker}')
        return
    if source.count(old) != 1:
        raise ValueError(f'Expected one injection site: {marker}')
    path.write_text(source.replace(old, new))
old = '    public static void func_800145E4(CpuContext c, IMemory m)\n    {'
inject('main.cs', old, old+'\n        if (c.RA == 0x80109DCCu && RecompOne.Runtime.Dispatch.Dispatcher.ActiveNames.Contains("boot")) Chronicles.Extras.PresentGallery(c, m);', 'Chronicles.Extras.PresentGallery')
old = '    public static void func_800289E8(CpuContext c, IMemory m)\n    {'
inject('main.cs', old, old+'\n        Chronicles.Extras.BeginMovie(c.A0);', 'Chronicles.Extras.BeginMovie')
# Match the unique movie input call using its return address, preserving PGXP instrumentation.
p = root / 'generated/main.cs'
s = p.read_text()
a = s.index('        c.RA = 0x80028CE4u;')
b = s.index('        c.V1 = c.V0;', a)
old = s[a:b]
inject('main.cs', old, old+'        c.V0 = Chronicles.Extras.FilterMovieButtons(c.V0);\n', 'Chronicles.Extras.FilterMovieButtons') if 'Chronicles.Extras.FilterMovieButtons' not in s else None
if p.read_text().count('Chronicles.Extras.FilterMovieButtons(c.V0);') != 1:
    raise ValueError('Unexpected movie input patch')
old = '        L80028FCC: ;'
inject('main.cs', old, old+'\n        Chronicles.Extras.WaitForMovieFrame();', 'Chronicles.Extras.WaitForMovieFrame')
print('Extras: verified gallery frame pump, interview release gate and bounded movie wait')
