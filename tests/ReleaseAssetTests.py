"""Exercise the packaging filename contract without producing release archives."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from tools.release_assets import installer_asset_name


class ReleaseAssetTests(unittest.TestCase):
    def test_numeric_transition_preserves_05_bridge_releases(self):
        for version, expected in (
            ('0.5.6', 'DeskPet-Setup-0.5.6.exe'),
            ('0.5.11', 'DeskPet-Setup-0.5.11.exe'),
            ('0.6.0', 'DeskPet-Windows-Setup-0.6.0.exe'),
            ('1.0.0', 'DeskPet-Windows-Setup-1.0.0.exe'),
        ):
            with self.subTest(version=version):
                self.assertEqual(installer_asset_name(version), expected)

    def test_invalid_versions_cannot_become_paths(self):
        for value in ('', '../0.6.0', '0.6.0.exe', '0.6.0\n', '2147483648.0.0', '9' * 5000):
            with self.subTest(version=value[:30]):
                with self.assertRaises(ValueError):
                    installer_asset_name(value)


if __name__ == '__main__':
    unittest.main()
