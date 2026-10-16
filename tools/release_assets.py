"""Windows release filename contract shared by packaging and its smoke tests."""
import re


def installer_asset_name(version: str) -> str:
    value = version.lstrip('vV')
    if len(value) > 64 or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?', value):
        raise ValueError('Invalid release version')
    parts = tuple(int(part) for part in value.split('.'))
    if any(part > 2147483647 for part in parts):
        raise ValueError('Invalid release version')
    prefix = 'DeskPet-Windows-Setup-' if parts >= (0, 6, 0) else 'DeskPet-Setup-'
    return prefix + value + '.exe'
