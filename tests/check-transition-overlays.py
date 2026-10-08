#!/usr/bin/env python3
"""Check the overlay used when stage one ends after the bat fight."""
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]


class TransitionOverlayChecks(unittest.TestCase):
    def test_all_stage_entries_are_registered(self):
        # Main's eight-entry table at 0x80010000 selects these entrypoints.
        entries = [(171,57,0x801021F4),(228,66,0x8010154C),(294,56,0x80101BCC),
                   (350,56,0x80101794),(406,55,0x8010148C),(461,55,0x80101A04),
                   (516,66,0x80102254),(582,68,0x801026BC)]
        config = json.loads((ROOT / 'config/CastlevaniaChronicles.json').read_text())
        for lba, sectors, address in entries:
            with self.subTest(lba=lba):
                overlays = [o for o in config['overlays'] if o['offset'] == (lba-171)*2048]
                self.assertEqual(len(overlays), 1)
                self.assertEqual(overlays[0]['size'], sectors*2048)
                source = (ROOT/'generated'/f"{overlays[0]['name']}.cs").read_text()
                self.assertIn(f'[{address:#010X}u]'.replace('0X','0x'), source)

    def test_post_bat_block_has_dispatch_entry(self):
        config = json.loads((ROOT / 'config/CastlevaniaChronicles.json').read_text())
        overlays = [item for item in config['overlays'] if item['offset'] == (711 - 171) * 2048]
        self.assertEqual(len(overlays), 1, 'archive block 10 at LBA 711 must be registered')
        overlay = overlays[0]
        self.assertEqual(overlay['size'], 11 * 2048)
        self.assertEqual(overlay['base'], '0x80100000')
        source = (ROOT / 'generated' / f"{overlay['name']}.cs").read_text()
        self.assertRegex(source, re.compile(r'\[0x80100FE0u\]\s*=\s*[^,]+'))
        self.assertRegex(source, re.compile(r'public static void func_80100FE0'))


if __name__ == '__main__':
    unittest.main()
