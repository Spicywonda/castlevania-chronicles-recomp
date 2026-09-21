#!/usr/bin/env python3
import collections
import json
import struct
import sys
from pathlib import Path
record = struct.Struct('<qIIiQQ')
data = Path(sys.argv[1]).read_bytes()
if len(data) % record.size:
    raise SystemExit('Incomplete block trace')
seen = {}
counts = collections.Counter()
repeats = []
for sample, address, generation, voice, lo, hi in record.iter_unpack(data):
    counts[voice] += 1
    key = (voice, address)
    old = seen.get(key)
    if generation and old == generation and 0x1010 <= address < 0x1810:
        repeats.append({'seconds':round(sample / 44100, 6), 'voice':voice,
                        'address':hex(address),'generation':generation})
    seen[key] = generation
report = {'blocks_by_voice':dict(counts),'unchanged_generation_reads':len(repeats),
          'first_repeats':repeats[:30]}
print(json.dumps(report, indent=2))
