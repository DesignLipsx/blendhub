"""
scrape_blender_windows.py
--------------------------
Scrapes https://download.blender.org/release/ and stores all Windows
installer URLs with their release dates and file sizes in a SQLite database.

Zero external dependencies — uses only Python standard library modules:
  - urllib.request  (HTTP)
  - html.parser     (HTML parsing)
  - sqlite3         (database)
  - re, time        (regex, polite delay)

Usage:
    python scrape_blender_windows.py

Output:
    blender_versions_web.db  (matches BlendHub C# app schema)
"""

import re
import time
import sqlite3
import urllib.request
from html.parser import HTMLParser

BASE_URL   = "https://download.blender.org/release/"
OUTPUT_DB  = "blender_versions_web.db"

SKIP_DIRS  = {
    "BlenderBenchmark1.0",
    "BlenderBenchmark2.0",
    "Publisher2.25",
    "plugin",
    "yafray.0.0.6",
    "yafray.0.0.7",
}

WINDOWS_EXTENSIONS = {".exe", ".msi", ".msix", ".zip"}


# ---------------------------------------------------------------------------
# HTML parser – extracts entries from Apache-style directory listings
# ---------------------------------------------------------------------------
class DirectoryListingParser(HTMLParser):
    """
    Parses an Apache directory listing page.
    Each row looks like:
        <a href="filename">filename</a>   DD-Mon-YYYY HH:MM   SIZE
    """

    def __init__(self, base_url: str):
        super().__init__()
        self.base_url   = base_url
        self.entries    = []          # list of dicts
        self._current_href = None     # href of the <a> we're inside

    def handle_starttag(self, tag, attrs):
        if tag == "a":
            attrs_dict = dict(attrs)
            href = attrs_dict.get("href", "")
            if href and href not in ("../", "/") and not href.startswith("?"):
                self._current_href = href

    def handle_endtag(self, tag):
        if tag == "a":
            self._current_href = None

    def handle_data(self, data):
        # Data that arrives while we are NOT inside an <a> tag but immediately
        # after one is the date/size column — we detect it via regex.
        if self._current_href is not None:
            return  # still inside the <a> tag

        m = re.search(
            r"(\d{2}-\w{3}-\d{4}\s+\d{2}:\d{2})\s+([\d]+|-)", data
        )
        if m and self.entries:
            # Attach date/size to the most recently added entry
            last = self.entries[-1]
            if not last["date"]:          # only fill if not already set
                last["date"]       = m.group(1).strip()
                size_raw           = m.group(2).strip()
                last["size_bytes"] = int(size_raw) if size_raw.isdigit() else None

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)


def _flush_href(parser: DirectoryListingParser, href: str):
    """Called right after an </a> to register the href as a new entry."""
    pass


# Simpler approach: override feed() to capture (href, trailing_text) pairs
class _AnchorTextParser(HTMLParser):
    """Collects (href, text_after_anchor) pairs from a directory listing."""

    def __init__(self, base_url: str):
        super().__init__()
        self.base_url = base_url
        self.entries  = []
        self._href    = None
        self._collect = False     # True right after </a>

    def handle_starttag(self, tag, attrs):
        if tag == "a":
            d = dict(attrs)
            href = d.get("href", "")
            if href and href not in ("../", "/") and not href.startswith("?"):
                self._href    = href
                self._collect = False

    def handle_endtag(self, tag):
        if tag == "a" and self._href:
            # Register a new entry; date/size filled in on next data chunk
            name = self._href.rstrip("/")
            self.entries.append({
                "name":       name,
                "url":        self.base_url + self._href,
                "date":       "",
                "size_bytes": None,
            })
            self._collect = True   # next data node is the date/size text
            self._href    = None

    def handle_data(self, data):
        if not self._collect or not self.entries:
            return
        m = re.search(r"(\d{2}-\w{3}-\d{4}\s+\d{2}:\d{2})\s+([\d]+|-)", data)
        if m:
            last               = self.entries[-1]
            last["date"]       = m.group(1).strip()
            size_raw           = m.group(2).strip()
            last["size_bytes"] = int(size_raw) if size_raw.isdigit() else None
        self._collect = False


# ---------------------------------------------------------------------------
# HTTP helper
# ---------------------------------------------------------------------------
def fetch_page(url: str) -> str | None:
    """Fetch URL and return HTML as a string, or None on failure."""
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "BlenderScraper/1.0"})
        with urllib.request.urlopen(req, timeout=15) as resp:
            return resp.read().decode("utf-8", errors="replace")
    except Exception as e:
        print(f"  [ERROR] {url}: {e}")
        return None


def parse_index(html: str, base_url: str) -> list[dict]:
    parser = _AnchorTextParser(base_url)
    parser.feed(html)
    return parser.entries


# ---------------------------------------------------------------------------
# Windows installer detection
# ---------------------------------------------------------------------------
def is_windows_installer(filename: str) -> bool:
    lower = filename.lower()
    has_keyword = bool(re.search(r"windows|_win", lower))
    ext         = "." + lower.rsplit(".", 1)[-1] if "." in lower else ""
    return has_keyword and ext in WINDOWS_EXTENSIONS


def version_from_dirname(dirname: str) -> str:
    m = re.match(r"Blender(.+)", dirname, re.IGNORECASE)
    return m.group(1) if m else dirname


# ---------------------------------------------------------------------------
# SQLite helpers (Schema matches BlendHub C# app expectations)
# ---------------------------------------------------------------------------
def init_db(conn: sqlite3.Connection) -> None:
    """Create tables matching BlendHub C# app schema."""
    conn.executescript("""
        CREATE TABLE IF NOT EXISTS Versions (
            Id   TEXT PRIMARY KEY,
            Version   TEXT NOT NULL,
            Directory TEXT NOT NULL,
            LastUpdated TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Installers (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            VersionId    TEXT NOT NULL REFERENCES Versions(Id),
            Filename     TEXT NOT NULL,
            Url          TEXT NOT NULL,
            ReleaseDate  TEXT NOT NULL,
            SizeBytes    INTEGER NOT NULL
        );
        
        CREATE INDEX IF NOT EXISTS idx_version_id ON Installers(VersionId);
    """)
    conn.commit()


def upsert_version(conn: sqlite3.Connection, version_id: str, version: str, directory: str) -> None:
    """Insert or replace version with TEXT Id (e.g., '4.2', '3.6')."""
    from datetime import datetime
    last_updated = datetime.now().isoformat()
    conn.execute(
        """INSERT OR REPLACE INTO Versions (Id, Version, Directory, LastUpdated)
           VALUES (?, ?, ?, ?)""",
        (version_id, version, directory, last_updated),
    )
    conn.commit()


def insert_installer(
    conn: sqlite3.Connection,
    version_id: str,
    filename: str,
    url: str,
    release_date: str,
    size_bytes: int,
) -> None:
    """Insert installer linked to version by TEXT VersionId."""
    conn.execute(
        """INSERT OR IGNORE INTO Installers
               (VersionId, Filename, Url, ReleaseDate, SizeBytes)
           VALUES (?, ?, ?, ?, ?)""",
        (version_id, filename, url, release_date or "Unknown", size_bytes or 0),
    )
    conn.commit()


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main() -> None:
    print(f"Fetching top-level index: {BASE_URL}")
    html = fetch_page(BASE_URL)
    if not html:
        print("Failed to fetch main index. Aborting.")
        return

    top_entries  = parse_index(html, BASE_URL)
    version_dirs = [
        e for e in top_entries
        if e["name"].startswith("Blender") and e["name"] not in SKIP_DIRS
    ]
    print(f"Found {len(version_dirs)} Blender version directories.\n")

    conn = sqlite3.connect(OUTPUT_DB)
    init_db(conn)

    total_versions    = 0
    total_installers  = 0

    for vdir in version_dirs:
        dirname = vdir["name"]
        version = version_from_dirname(dirname)
        version_id = VersionHelper.get_short_version(version)  # e.g., "4.2"
        dir_url = vdir["url"]

        print(f"  Scanning {dirname} (version_id={version_id}) ...")
        sub_html = fetch_page(dir_url)
        if not sub_html:
            continue

        files         = parse_index(sub_html, dir_url)
        windows_files = [f for f in files if is_windows_installer(f["name"])]

        if windows_files:
            upsert_version(conn, version_id, version, dirname)
            for f in windows_files:
                insert_installer(
                    conn, version_id,
                    f["name"], f["url"], f["date"], f["size_bytes"],
                )
            count = len(windows_files)
            print(f"    -> {count} Windows file(s) stored.")
            total_versions   += 1
            total_installers += count
        else:
            print("    -> No Windows installers found.")

        time.sleep(0.3)   # Be polite to the server

    conn.close()

    print(f"\nDone! {total_versions} versions with Windows installers "
          f"({total_installers} files total).")
    print(f"Output saved to: {OUTPUT_DB}")


class VersionHelper:
    """Matches C# VersionHelper logic."""
    
    @staticmethod
    def get_short_version(full_version: str) -> str:
        """Convert '5.1.0' or '5.1.0 LTS' to '5.1'."""
        clean = full_version.replace("LTS", "").strip()
        parts = clean.split('.')
        if len(parts) >= 2:
            return f"{parts[0]}.{parts[1]}"
        return clean


if __name__ == "__main__":
    main()
    input("\nPress Enter to exit...")
