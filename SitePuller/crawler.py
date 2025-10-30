#!/usr/bin/env python3
import asyncio
import argparse
import json
import os
import re
import sys
from urllib.parse import urljoin, urlparse, urldefrag

import aiohttp
from bs4 import BeautifulSoup
from playwright.async_api import async_playwright, TimeoutError as PWTimeout

from playwright.async_api import TimeoutError as PWTimeout

async def do_login(context, login_url: str, user_env: str, pass_env: str,
                   user_selector: str, pass_selector: str, submit_selector: str,
                   post_login_url: str | None, ready_selector: str | None,
                   storage_state_path: str | None):
    user = os.environ.get(user_env or "", "")
    pwd = os.environ.get(pass_env or "", "")
    if not user or not pwd:
        raise RuntimeError(f"Missing credentials: ensure env vars {user_env=} and {pass_env=} are set")

    print('Performing login...')

    page = await context.new_page()
    try:
        await page.goto(login_url, wait_until="domcontentloaded", timeout=45000)
        await page.fill(user_selector, user)
        await page.fill(pass_selector, pwd)
        await page.click(submit_selector)

        print('Submitted login form, waiting for post-login state...')

        # Wait for success: either URL change or a known element
        if post_login_url:
            try:
                await page.wait_for_url(post_login_url, timeout=20000)
                print('Submitted login form, waiting for post-login state...')
            except PWTimeout:
                print('Could not login.')

            print('User Logged In')
            
        if ready_selector:
            await page.wait_for_selector(ready_selector, timeout=20000)

        # Optionally confirm you are logged in (e.g., presence of logout button)
        # await page.wait_for_selector("text=Logout", timeout=10000)

        if storage_state_path:
            await context.storage_state(path=storage_state_path)
    finally:
        await page.close()

ANGULAR_IDLE_SCRIPT = """
() => new Promise(resolve => {
  const timeoutQuietMs = %d;
  let lastChange = performance.now();
  const obs = new MutationObserver(() => { lastChange = performance.now(); });
  obs.observe(document, {subtree: true, childList: true, attributes: true, characterData: true});
  function check() {
    if (performance.now() - lastChange >= timeoutQuietMs) {
      obs.disconnect();
      resolve();
    } else {
      requestAnimationFrame(check);
    }
  }
  check();
})
"""

DISABLE_SCROLL_SCRIPT = """
() => {
    const contentSection = document.querySelector('.nv-content');
    contentSection.classList.remove('nv-content');

    const footerSection = document.querySelector('.footer-bar');
    footerSection.classList.remove('footer-bar');
    footerSection.classList.add('footer-bar-no-scroll');
    }
"""

# ----------------------------
# Helpers
# ----------------------------

def normalize_url(base_url: str, href: str) -> str | None:
    try:
        if not href:
            return None
        href = href.strip()
        # Ignore anchors and non-http links
        if href.startswith("#") or href.startswith("javascript:") or href.startswith("mailto:") or href.startswith("tel:"):
            return None
        absu = urljoin(base_url, href)
        absu, _frag = urldefrag(absu)
        p = urlparse(absu)
        if p.scheme not in ("http", "https"):
            return None
        return absu
    except Exception:
        return None

def same_domain(u: str, root: str) -> bool:
    pu = urlparse(u)
    pr = urlparse(root)
    return pu.netloc == pr.netloc

def ensure_dir(path: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)

def safe_filename(url: str) -> str:
    p = urlparse(url)
    path = p.path if p.path else "/"
    if path.endswith("/"):
        path += "index"
    base = f"{p.netloc}{path}"
    base = re.sub(r"[^A-Za-z0-9._-]", "_", base)
    if len(base) > 150:
        base = base[:150]
    return base

def extract_text(html: str) -> str:
    soup = BeautifulSoup(html, "html.parser")
    for tag in soup(["script", "style", "noscript"]):
        tag.decompose()
    text = soup.get_text(" ", strip=True)
    text = re.sub(r"\s{2,}", " ", text)
    return text

async def fetch_sitemap_urls(sitemap_url: str) -> list[str]:
    urls: list[str] = []
    try:
        async with aiohttp.ClientSession() as session:
            async with session.get(sitemap_url, timeout=30) as resp:
                if resp.status != 200:
                    return urls
                xml = await resp.text()
                # simple extraction of <loc>...</loc>
                for m in re.finditer(r"<loc>\s*([^<]+)\s*</loc>", xml):
                    urls.append(m.group(1).strip())
    except Exception as e:
        sys.stderr.write(f"Failed to load sitemap {sitemap_url}: {e}\n")
    return urls

# ----------------------------
# SPA/Angular helpers
# ----------------------------

ANGULAR_IDLE_SCRIPT = """
() => new Promise(resolve => {
  const timeoutQuietMs = %d;
  let lastChange = performance.now();
  const obs = new MutationObserver(() => { lastChange = performance.now(); });
  obs.observe(document, {subtree: true, childList: true, attributes: true, characterData: true});
  function check() {
    if (performance.now() - lastChange >= timeoutQuietMs) {
      obs.disconnect();
      resolve();
    } else {
      requestAnimationFrame(check);
    }
  }
  check();
})
"""

async def wait_for_spa_settle(page, quiet_ms: int, ready_selector: str | None):
    # Wait for network to idle best-effort
    try:
        await page.wait_for_load_state("networkidle", timeout=10000)
    except PWTimeout:
        pass
    # Optional: wait for a known selector when provided
    if ready_selector:
        try:
            await page.wait_for_selector(ready_selector, timeout=15000)
        except PWTimeout:
            pass
    # Heuristic: wait for DOM to be quiet for quiet_ms
    try:
        await page.evaluate(ANGULAR_IDLE_SCRIPT % quiet_ms)
    except Exception:
        await page.wait_for_timeout(quiet_ms)

async def discover_links(page, base_url: str) -> list[str]:
    # Collect standard anchors first
    anchors = await page.eval_on_selector_all("div[pageroute]", "els => els.map(e => e.getAttribute('pageroute'))")
    routes = [h for h in anchors if h]

    hrefs: list[str] = []

    for route in routes:
        hrefs.append(route)

    print(hrefs)

    # Angular-specific routerLink attributes (best-effort; may be tree-shaken in prod)
    try:
        rlinks = await page.eval_on_selector_all("[routerLink],[ng-reflect-router-link]", "els => els.map(e => e.getAttribute('routerLink') || e.getAttribute('ng-reflect-router-link'))")
        for rl in rlinks or []:
            if rl:
                hrefs.append(rl)
    except Exception:
        pass

    # Normalize to absolute URLs
    out: list[str] = []
    for href in hrefs:
        nu = normalize_url(base_url, href)
        if nu:
            out.append(nu)
    # De-dup
    return list(dict.fromkeys(out))

async def smart_scroll(page):
    # Trigger lazy loads via incremental scroll
    await page.evaluate("""async () => {
        const step = 800;
        const max = Math.max(document.body.scrollHeight, document.documentElement.scrollHeight);
        let y = 0;
        while (y < max) {
            window.scrollTo(0, y);
            await new Promise(r => setTimeout(r, 150));
            y += step;
        }
        window.scrollTo(0, 0);
    }""")

# ----------------------------
# Crawler
# ----------------------------

async def crawl(start_url: str, outdir: str, max_pages: int, same_domain_only: bool, delay: float,
                include_regex: str | None, exclude_regex: str | None,
                quiet_ms: int, ready_selector: str | None, routes_file: str | None, sitemap: str | None,
                unfix_fixed: bool, hide_selectors: str | None, expand_scrollables: bool):
    visited: set[str] = set()
    queue: list[str] = [start_url]

    # Seed from routes file
    if routes_file and os.path.exists(routes_file):
        with open(routes_file, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line:
                    url = normalize_url(start_url, line)
                    if url:
                        queue.append(url)

    # Seed from sitemap.xml
    if sitemap:
        for u in await fetch_sitemap_urls(sitemap):
            queue.append(u)

    jsonl_path = os.path.join(outdir, "pages.jsonl")
    csv_path = os.path.join(outdir, "sitemap.csv")
    os.makedirs(outdir, exist_ok=True)

    inc = re.compile(include_regex) if include_regex else None
    exc = re.compile(exclude_regex) if exclude_regex else None

    async with async_playwright() as p:
        storage_state_path = args.storage_state if args.storage_state else None
        context_kwargs = {
            "viewport": {"width": 1600, "height": 1000},
            "user_agent": "SiteCrawler/1.2 (+LLM ingestion)"
        }
        if storage_state_path and os.path.exists(storage_state_path):
            context_kwargs["storage_state"] = storage_state_path

        browser = await p.chromium.launch(headless=True)
        context = await browser.new_context(
            viewport={"width": 1600, "height": 1000},
            user_agent="SiteCrawler/1.1 (+LLM ingestion)"
        )

        if args.login_url:
            await do_login(
                context=context,
                login_url=args.login_url,
                user_env=args.user_env,
                pass_env=args.pass_env,                
                user_selector=args.user_selector,
                pass_selector=args.pass_selector,
                submit_selector=args.submit_selector,
                post_login_url=args.post_login_url,
                ready_selector=args.ready_selector,  # reuse your existing ready selector
                storage_state_path=storage_state_path
            )
        else:
            print("Skipping login; using existing storage state if available.")

        with open(jsonl_path, "w", encoding="utf-8") as jf, open(csv_path, "w", encoding="utf-8") as cf:
            cf.write("url,title,status,screenshot_path,html_path,text_path\n")
            processed = 0
            while queue and processed < max_pages:
                url = queue.pop(0)
                print(f"Requesting page {url}") 
                if url in visited:
                    continue
                if same_domain_only and not same_domain(url, start_url):
                    continue
                if inc and not inc.search(url):
                    continue
                if exc and exc.search(url):
                    continue

                visited.add(url)
                page = await context.new_page()
                status = None
                try:
                    resp = await page.goto(url, wait_until="domcontentloaded", timeout=60000)
                    status = resp.status if resp else None

                    await wait_for_spa_settle(page, quiet_ms=quiet_ms, ready_selector=ready_selector)
                    await smart_scroll(page)
                    await wait_for_spa_settle(page, quiet_ms=min(quiet_ms, 1200), ready_selector=None)

                    title = await page.title()

                    _hide = []
                    if hide_selectors:
                        _hide = [s.strip() for s in hide_selectors.split(",") if s.strip()]
                    _unfix = bool(unfix_fixed)
                    _expand = bool(expand_scrollables)
                    await page.evaluate(DISABLE_SCROLL_SCRIPT)

                    filebase = safe_filename(url)
                    shot_path = os.path.join(outdir, "screens", f"{filebase}.png")
                    html_path = os.path.join(outdir, "html", f"{filebase}.html")
                    text_path = os.path.join(outdir, "text", f"{filebase}.txt")

                    os.makedirs(os.path.dirname(shot_path), exist_ok=True)
                    os.makedirs(os.path.dirname(html_path), exist_ok=True)
                    os.makedirs(os.path.dirname(text_path), exist_ok=True)

                    await page.screenshot(path=shot_path, full_page=True)
                    html = await page.content()
                    with open(html_path, "w", encoding="utf-8") as f:
                        f.write(html)
                    text = extract_text(html)
                    with open(text_path, "w", encoding="utf-8") as f:
                        f.write(text)

                    rec = {
                        "url": url,
                        "title": title,
                        "status": status,
                        "screenshot_path": os.path.relpath(shot_path, outdir),
                        "html_path": os.path.relpath(html_path, outdir),
                        "text_path": os.path.relpath(text_path, outdir),
                    }
                    jf.write(json.dumps(rec, ensure_ascii=False) + "\n")
                    cf.write(f"{json.dumps(url)},{json.dumps(title)},{status},{json.dumps(rec['screenshot_path'])},{json.dumps(rec['html_path'])},{json.dumps(rec['text_path'])}\n")

                    processed += 1

                    links = await discover_links(page, url)
                    for nu in links:
                        if nu not in visited:
                            if same_domain_only and not same_domain(nu, start_url):
                                continue
                            if inc and not inc.search(nu):
                                continue
                            if exc and exc.search(nu):
                                continue
                            queue.append(nu)

                except Exception as e:
                    sys.stderr.write(f"Error on {url}: {e}\n")
                finally:
                    await page.close()

                if delay > 0:
                    await asyncio.sleep(delay)

        await context.close()
        await browser.close()

def parse_args():
    ap = argparse.ArgumentParser(description="Crawl a website (incl. Angular SPAs) and save screenshots, HTML, and text for LLM ingestion.")
    ap.add_argument("start_url", help="Starting URL (e.g., https://example.com)")
    ap.add_argument("-o", "--outdir", default="site_dump", help="Output directory (default: site_dump)")
    ap.add_argument("-n", "--max-pages", type=int, default=200, help="Max pages to crawl (default: 200)")
    ap.add_argument("--all-domains", action="store_true", help="Allow leaving the start domain (default: false)")
    ap.add_argument("--delay", type=float, default=0.5, help="Delay between pages in seconds (default: 0.5)")
    ap.add_argument("--include", help="Regex to include only URLs that match (applied to absolute URLs)")
    ap.add_argument("--exclude", help="Regex to exclude URLs that match (applied to absolute URLs)")
    ap.add_argument("--quiet-ms", type=int, default=2000, help="DOM idle threshold in ms for SPA settling (default: 2000)")
    ap.add_argument("--ready-selector", help="CSS selector that must appear before capture (e.g., app root or page hero)")
    ap.add_argument("--routes-file", help="Seed crawl with additional routes (one per line, absolute or relative to start_url)")
    ap.add_argument("--sitemap", help="Seed crawl with URLs from a sitemap.xml (absolute URL)")
    ap.add_argument("--login-url", help="Login page URL")
    ap.add_argument("--user-env", help="Env var for username/email (e.g., CRAWL_USER)")
    ap.add_argument("--pass-env", help="Env var for password (e.g., CRAWL_PASS)")
    ap.add_argument("--user-selector", default='input[type="email"]', help="CSS selector for the email/username field")
    ap.add_argument("--pass-selector", default='input[type="password"]', help="CSS selector for the password field")
    ap.add_argument("--submit-selector", default='button[type="submit"]', help="CSS selector for the submit button")
    ap.add_argument("--post-login-url", help="URL or pattern Playwright should reach after successful login")
    ap.add_argument("--storage-state", default="storageState.json", help="Path to save/reuse Playwright storage state")
    ap.add_argument("--unfix-fixed", action="store_true", help="Convert fixed/sticky headers/footers to static before capture")
    ap.add_argument("--hide-selectors", help="Comma-separated CSS selectors to hide (e.g., .cookie, .chat-widget)")
    ap.add_argument("--expand-scrollables", action="store_true", help="Expand overflowed scroll containers to reveal hidden content")

    return ap.parse_args()

if __name__ == "__main__":
    args = parse_args()
    same_domain_only = not args.all_domains
    asyncio.run(crawl(
        start_url=args.start_url,
        outdir=args.outdir,
        max_pages=args.max_pages,
        same_domain_only=same_domain_only,
        delay=args.delay,
        include_regex=args.include,
        exclude_regex=args.exclude,
        quiet_ms=args.quiet_ms,
        ready_selector=args.ready_selector,
        routes_file=args.routes_file,
        sitemap=args.sitemap,
        unfix_fixed=args.unfix_fixed,
        hide_selectors=args.hide_selectors,
        expand_scrollables=args.expand_scrollables
    ))
