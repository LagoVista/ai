
# Website Snapshot & Text Pack (for LLM ingestion)

This simple crawler uses **Playwright** to visit pages on your site, take **full‑page PNG screenshots**, save **raw HTML**, extract **plain text**, and write a **JSONL** file—perfect for feeding into an LLM or RAG index.

## What you get
- `site_dump/screens/*.png` – full-page screenshots for visual review or image-based context
- `site_dump/html/*.html` – raw HTML for each page
- `site_dump/text/*.txt` – plain-text extract (scripts/styles removed)
- `site_dump/pages.jsonl` – one JSON record per page with title, status, and relative file paths
- `site_dump/sitemap.csv` – quick view of URL → files

## Install

> Requires Python 3.10+

```bash
python -m venv .venv
# Windows: .venv\Scripts\activate
# macOS/Linux:
source .venv/bin/activate

pip install -r requirements.txt
python -m playwright install
```

## Run

```bash
python crawler.py "https://YOURDOMAIN.com" -o site_dump -n 300 --delay 0.5
```

**Common flags:**
- `-n / --max-pages` – cap the crawl
- `--include` – only crawl URLs matching this regex
- `--exclude` – skip URLs matching this regex
- `--all-domains` – allow navigating off the start domain (defaults to same-domain only)
- `--delay` – small pause between pages to be polite and allow lazy content to load

**Examples:**

Only crawl docs pages:
```bash
python crawler.py "https://example.com" --include "/docs/" -n 100
```

Skip media-heavy paths:
```bash
python crawler.py "https://example.com" --exclude "/media/|/assets/" -n 200
```

## Tips for LLM use

- Feed `pages.jsonl` straight into your indexing job (e.g., one record per page). Each record includes:
  ```json
  {"url":"https://example.com/docs/getting-started",
   "title":"Getting Started — Example",
   "status":200,
   "screenshot_path":"screens/example.com_docs_getting-started.png",
   "html_path":"html/example.com_docs_getting-started.html",
   "text_path":"text/example.com_docs_getting-started.txt"}
  ```
- If you maintain a **sitemap.xml**, run with `--include` to bias crawling toward those routes.
- For authenticated sections, add a small login step before crawling—e.g., navigate to a login page, fill credentials, and continue. (This simple starter doesn’t include auth flows; if you want, I can add a login routine that reads creds from env vars and executes CSS‑selector steps.)

## Notes

- The crawler uses a single browser tab and BFS traversal. It attempts to scroll the page to trigger lazy-loaded content before screenshotting.
- This script is intentionally simple and safe to modify. If you need concurrency, PDFs, or API capture, those can be added.
- Please respect your own `robots.txt` policies and rate-limit as needed.


## Angular / SPA notes

This version is tuned for Angular SPAs:

- Discovers links from both standard anchors and Angular router links (`[routerLink]`, `ng-reflect-router-link`) when present.
- Waits for the app to settle using a **DOM-idle heuristic** (no mutations for `--quiet-ms` milliseconds), plus an optional **--ready-selector** if you want to wait for a specific element to appear.
- Supports seeding the queue with:
  - `--routes-file routes.txt` (one path per line, e.g. `/docs/getting-started`)
  - `--sitemap https://yourdomain.com/sitemap.xml`

**Example**

```bash
python crawler.py "https://yourdomain.com" -n 500 --quiet-ms 2500 --ready-selector "app-root" --routes-file routes.txt --include "/(docs|product)/"
```

If your Angular build strips `routerLink` attributes in production, use `--routes-file` or `--sitemap` to enumerate routes explicitly.
