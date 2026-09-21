#!/usr/bin/env python3
import json
import sys
from pathlib import Path
report = json.loads(Path(sys.argv[1]).read_text())
spu = report['Spu']
if report['Seconds'] < 60 or spu['IrqCount'] < 100:
    raise SystemExit('INCOMPLETE: need a minute of capture with an active audio stream')
late = spu['DeliveriesOver896Samples']
print(f"IRQ deliveries: {spu['IrqCount']}; late: {late}; max delay: {spu['MaxDeliveryDelaySamples'] / 44.1:.2f} ms")
raise SystemExit(1 if late else 0)
