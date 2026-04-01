"""
scrape_blender_windows.py
--------------------------
Scrapes https://download.blender.org/release/ and builds a JSON file of
all Windows installer URLs with their release dates and file sizes.

Usage:
    pip install requests beautifulsoup4
    python scrape_blender_windows.py

Output:
    blender_windows_installers.json
"""

import re
import json
import time
import requests
from bs4 import BeautifulSoup
from datetime import datetime

BASE_URL = "https://download.blender.org/release/"

# Folders to skip (non-Blender-release directories)
SKIP_DIRS = {
    "BlenderBenchmark1.0",
    "BlenderBenchmark2.0",
    "Publisher2.25",
    "plugin",
    "yafray.0.0.6",
    "yafray.0.0.7",
}

# Only keep files that look like Windows installers
WINDOWS_PATTERN = re.compile(r"windows", re.IGNORECASE)
WINDOWS_EXTENSIONS = {".exe", ".msi", ".msix", ".zip"}


def fetch_page(url: str) -> BeautifulSoup | None:
    """Fetch a URL and return a BeautifulSoup object, or None on failure."""
    try:
        resp = requests.get(url, timeout=15)
        resp.raise_for_status()
        return BeautifulSoup(resp.text, "html.parser")
    except requests.RequestException as e:
        print(f"  [ERROR] {url}: {e}")
        return None


def parse_index_page(soup: BeautifulSoup, base_url: str) -> list[dict]:
    """
    Parse an Apache-style directory listing page.
    Returns a list of dicts: {name, url, date, size}
    for every <a> link that isn't a parent directory.
    """
    entries = []
    pre = soup.find("pre")
    if not pre:
        return entries

    # Each file row looks like:
    # <a href="filename">filename</a>   DD-Mon-YYYY HH:MM   SIZE
    for a_tag in pre.find_all("a"):
        href = a_tag.get("href", "")
        if href in ("../", "/"):
            continue  # skip parent link

        name = href.rstrip("/")
        full_url = base_url + href

        # Grab the raw text that follows the </a> tag on the same line
        # BeautifulSoup keeps it as a NavigableString sibling
        raw = ""
        sibling = a_tag.next_sibling
        if sibling:
            raw = str(sibling)

        # Parse date and size from "   DD-Mon-YYYY HH:MM   SIZE"
        date_str = ""
        size_str = ""
        m = re.search(
            r"(\d{2}-\w{3}-\d{4}\s+\d{2}:\d{2})\s+([\d\-]+)", raw
        )
        if m:
            date_str = m.group(1).strip()
            size_str = m.group(2).strip()

        entries.append(
            {
                "name": name,
                "url": full_url,
                "date": date_str,
                "size_bytes": int(size_str) if size_str.isdigit() else None,
            }
        )

    return entries


def is_windows_installer(filename: str) -> bool:
    """Return True if the file is a Windows installer/archive."""
    lower = filename.lower()
    # Must contain 'windows' OR be an old-style .exe/.zip with no platform in name
    has_windows_keyword = "windows" in lower
    # Old releases like blender1.60_Windows.exe
    has_windows_in_name = bool(re.search(r"windows|_win", lower, re.IGNORECASE))
    # Check extension
    ext = "." + lower.rsplit(".", 1)[-1] if "." in lower else ""
    has_valid_ext = ext in WINDOWS_EXTENSIONS

    return has_valid_ext and has_windows_in_name


def get_version_from_dirname(dirname: str) -> str:
    """Extract version string from directory name like 'Blender3.6' -> '3.6'."""
    m = re.match(r"Blender(.+)", dirname, re.IGNORECASE)
    return m.group(1) if m else dirname


def main():
    print(f"Fetching top-level index: {BASE_URL}")
    soup = fetch_page(BASE_URL)
    if not soup:
        print("Failed to fetch main index. Aborting.")
        return

    # Get all version directories
    top_entries = parse_index_page(soup, BASE_URL)
    version_dirs = [
        e for e in top_entries
        if e["name"].startswith("Blender") and e["name"] not in SKIP_DIRS
    ]

    print(f"Found {len(version_dirs)} Blender version directories.\n")

    results = {}

    for vdir in version_dirs:
        dirname = vdir["name"]
        version = get_version_from_dirname(dirname)
        dir_url = vdir["url"]

        print(f"  Scanning {dirname} ...")
        vsoup = fetch_page(dir_url)
        if not vsoup:
            continue

        files = parse_index_page(vsoup, dir_url)
        windows_files = [f for f in files if is_windows_installer(f["name"])]

        if windows_files:
            results[version] = {
                "version": version,
                "directory": dirname,
                "windows_installers": [
                    {
                        "filename": f["name"],
                        "url": f["url"],
                        "release_date": f["date"],
                        "size_bytes": f["size_bytes"],
                    }
                    for f in windows_files
                ],
            }
            print(f"    -> {len(windows_files)} Windows file(s) found.")
        else:
            print(f"    -> No Windows installers found.")

        time.sleep(0.3)  # Be polite to the server

    output_path = "blender_windows_installers.json"
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2)

    total = sum(len(v["windows_installers"]) for v in results.values())
    print(f"\nDone! {len(results)} versions with Windows installers ({total} files total).")
    print(f"Output saved to: {output_path}")


if __name__ == "__main__":
    main()