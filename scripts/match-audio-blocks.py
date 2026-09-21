#!/usr/bin/env python3
import collections,json,mmap,struct,sys
from pathlib import Path
record=struct.Struct('<qIIiQQ')
rows=list(record.iter_unpack(Path(sys.argv[1]).read_bytes()))
def key(lo,hi):
    b=struct.pack('<QQ',lo,hi)
    return b[:1]+b[2:]  # Streaming changes the ADPCM loop flag byte.
keys={key(r[4],r[5]) for r in rows}
found={}
with open(sys.argv[2],'rb') as f, mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ) as disc:
    size=268107776
    for sector in range((size+2047)//2048):
        base=(8823+sector)*2352+24
        payload=disc[base:base+min(2048,size-sector*2048)]
        for off in range(0,len(payload)-15,16):
            b=payload[off:off+16]; k=b[:1]+b[2:]
            if k in keys:
                position=sector*2048+off
                found[k]=position if k not in found else -1
last={}; counts=collections.Counter(); anomalies=[]; mapped=0
for sample,address,generation,voice,lo,hi in rows:
    pos=found.get(key(lo,hi),-1)
    if pos<0:
        last.pop(voice,None)
        continue
    mapped+=1
    if voice in last:
        previous=last[voice];delta=pos-previous[0];counts[delta]+=1
        if delta<=0:
            anomalies.append(dict(seconds=round(sample/44100,6),voice=voice,delta=delta,
                source_offset=pos,previous_generation=previous[1],generation=generation,address=hex(address)))
    last[voice]=(pos,generation)
print(json.dumps(dict(trace_blocks=len(rows),uniquely_mapped=mapped,
    common_source_steps=counts.most_common(12),backward_or_repeated=len(anomalies),first_anomalies=anomalies[:40]),indent=2))
