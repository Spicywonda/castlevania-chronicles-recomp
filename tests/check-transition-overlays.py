#!/usr/bin/env python3
"""Check the overlay used when stage one ends after the bat fight."""
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]


class TransitionOverlayChecks(unittest.TestCase):
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
