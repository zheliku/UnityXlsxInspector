"""Build an Assets-installable Unity package using only the Python standard library.

Usage: python pack_unitypackage.py <UPM package directory> <output directory>
Existing .meta files preserve script references and Editor-only plugin settings.
Only the Assets root and the exported license texts need generated metadata.
"""

import gzip
import io
import json
from pathlib import Path
import re
import sys
import tarfile
import time
import uuid


ASSET_ROOT = "Assets/UnityXlsxInspector"


def generated_meta(path, folder=False):
    """Give packaging-only assets stable GUIDs across releases."""
    guid = uuid.uuid5(uuid.NAMESPACE_URL, f"https://github.com/zheliku/UnityXlsxInspector/{path}").hex
    folder_line = "folderAsset: yes\n" if folder else ""
    return f"fileFormatVersion: 2\nguid: {guid}\n{folder_line}DefaultImporter:\n  externalObjects: {{}}\n".encode()


def add_asset(archive, path, meta, content=None):
    """Write Unity's GUID/asset, GUID/asset.meta and GUID/pathname records."""
    guid = re.search(rb"^guid: ([0-9a-f]{32})$", meta, re.MULTILINE).group(1).decode()
    directory = tarfile.TarInfo(guid)
    directory.type = tarfile.DIRTYPE
    directory.mode = 0o755
    directory.mtime = int(time.time())
    archive.addfile(directory)
    records = {}
    if content is not None:
        records["asset"] = content
    records.update({"asset.meta": meta, "pathname": path.encode()})
    for name, data in records.items():
        entry = tarfile.TarInfo(f"{guid}/{name}")
        entry.size = len(data)
        entry.mode = 0o644
        entry.mtime = directory.mtime
        archive.addfile(entry, io.BytesIO(data))


def pack(package_dir, output_dir):
    """Export the shipped code, libraries, icon, documentation and licenses."""
    version = json.loads((package_dir / "package.json").read_text(encoding="utf-8"))["version"]
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / f"UnityXlsxInspector-{version}.unitypackage"
    # Unity uses the filename stored in the gzip header when extracting the tar.
    with (
        output.open("wb") as raw,
        gzip.GzipFile(filename="archtemp.tar", mode="wb", fileobj=raw) as compressed,
        tarfile.open(fileobj=compressed, mode="w", format=tarfile.GNU_FORMAT) as archive,
    ):
        add_asset(archive, ASSET_ROOT, generated_meta(ASSET_ROOT, folder=True))

        # Runtime and Editor use the same files and metadata as the UPM release.
        sources = []
        for directory in ("Editor", "Runtime"):
            source = package_dir / directory
            sources.extend([source, *source.rglob("*")])
        sources.extend(package_dir / name for name in (
            "README.md", "CHANGELOG.md", "LICENSE.md", "Third Party Notices.md"))
        for source in sorted(sources):
            if source.suffix == ".meta":
                continue
            path = f"{ASSET_ROOT}/{source.relative_to(package_dir).as_posix()}"
            meta = Path(f"{source}.meta").read_bytes().replace(b"\r\n", b"\n")
            content = None if source.is_dir() else source.read_bytes()
            if source.suffix == ".md":
                content = content.replace(b"ThirdPartyNotices~", b"ThirdPartyNotices")
            add_asset(archive, path, meta, content)

        # Unity ignores folders ending in '~'; expose the license texts in Assets.
        notices_path = f"{ASSET_ROOT}/ThirdPartyNotices"
        add_asset(archive, notices_path, generated_meta(notices_path, folder=True))
        for source in sorted((package_dir / "ThirdPartyNotices~").glob("*.txt")):
            path = f"{notices_path}/{source.name}"
            add_asset(archive, path, generated_meta(path), source.read_bytes())
    print(output)


if __name__ == "__main__":
    pack(Path(sys.argv[1]), Path(sys.argv[2]))
