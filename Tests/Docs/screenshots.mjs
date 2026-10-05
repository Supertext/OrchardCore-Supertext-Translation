#!/usr/bin/env node
/**
 * Takes the screenshots in docs/images from a FRESH local demo whose module talks to the
 * stand-in API (node stand-in.mjs, without STAND_IN_PREFIX). See docs/DEVELOPER.md -> Docs
 * screenshots.
 *
 *   BASE_URL=http://127.0.0.1:8096 DEMO_ADMIN_EMAIL=… DEMO_ADMIN_PASSWORD=… \
 *   DEMO_EDITOR_EMAIL=… DEMO_EDITOR_PASSWORD=… npm run screenshots
 *
 * The demo must run with SUPERTEXT_API_ENDPOINT pointing to the stand-in and WITHOUT
 * SUPERTEXT_API_KEY: the script stores a dummy key in Settings → Supertext like an
 * administrator would. The endpoint hint (a local URL) is hidden in the settings shot.
 */
import { chromium } from 'playwright';
import { mkdirSync } from 'node:fs';

const base = (process.env.BASE_URL || 'http://127.0.0.1:8096').replace(/\/$/, '');
const out = new URL('../../docs/images/', import.meta.url).pathname;
mkdirSync(out, { recursive: true });
for (const name of ['DEMO_ADMIN_EMAIL', 'DEMO_ADMIN_PASSWORD', 'DEMO_EDITOR_EMAIL', 'DEMO_EDITOR_PASSWORD']) {
  if (!process.env[name]) throw new Error(`${name} is not set`);
}

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });

async function session(email, password) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1, colorScheme: 'light' });
  const page = await context.newPage();
  await page.goto(`${base}/Login`);
  await page.fill('input[name$="UserName"]', email);
  await page.fill('input[type=password]', password);
  await page.click('button[type=submit]');
  await page.waitForLoadState('networkidle');
  if (page.url().includes('/Login')) throw new Error(`Login failed for ${email}`);
  return page;
}

const shot = async (page, file, locator, { pad = 8, maxHeight } = {}) => {
  await page.waitForTimeout(400); // toasts fade in
  const box = await page.locator(locator).first().boundingBox();
  // Start below the admin's fixed top bar.
  const top = Math.max(56, box.y - pad);
  const clip = { x: Math.max(0, box.x - pad), y: top, width: box.width + 2 * pad, height: box.y + box.height + pad - top };
  if (maxHeight) clip.height = Math.min(clip.height, maxHeight);
  await page.screenshot({ path: out + file, clip });
  console.log('wrote', file);
};
const closeToasts = (page) => page.evaluate(() => document.querySelectorAll('.toast').forEach((t) => t.remove()));

// --- Administrator: settings and cultures -------------------------------------------
const admin = await session(process.env.DEMO_ADMIN_EMAIL, process.env.DEMO_ADMIN_PASSWORD);
await admin.goto(`${base}/Admin/Settings/supertext`);
if (await admin.locator('input[name$="ApiKey"]').count()) {
  await admin.fill('input[name$="ApiKey"]', 'demo-key-for-screenshots');
}
await admin.click('.ta-content button[type=submit].btn-primary');
await admin.waitForLoadState('networkidle');
await admin.evaluate(() => document.querySelectorAll('.supertext-endpoint-env').forEach((e) => e.remove()));
await admin.setViewportSize({ width: 1280, height: 1150 });
await shot(admin, 'settings.png', '.ta-content');

await admin.setViewportSize({ width: 1280, height: 860 });
await admin.goto(`${base}/Admin/Settings/localization`);
await shot(admin, 'cultures.png', '.ta-content', { maxHeight: 520 });
await admin.context().close();

// --- Editor: translate with Supertext -----------------------------------------------
const page = await session(process.env.DEMO_EDITOR_EMAIL, process.env.DEMO_EDITOR_PASSWORD);
await page.goto(`${base}/Admin/Contents/ContentItems`);
await shot(page, 'content-list.png', '.ta-content', { maxHeight: 330 });

const articleRow = page.locator('li.list-group-item', { hasText: 'Translating with Supertext' });
await articleRow.locator('a.supertext-translate').click();
await page.waitForLoadState('networkidle');
await page.check('input[value="de-CH"]');
await shot(page, 'translate-before.png', '.ta-content', { maxHeight: 420 });

await page.click('button.supertext-submit');
await page.waitForLoadState('networkidle');
await shot(page, 'translate-after.png', '.ta-content', { maxHeight: 420 });
const translateUrl = page.url();

// The German draft in the editor
await closeToasts(page);
await page.click('tr:has(input[value="de-CH"]) a');
await page.waitForLoadState('networkidle');
await page.setViewportSize({ width: 1280, height: 1000 });
await shot(page, 'translated-editor.png', '.ta-content', { maxHeight: 760 });
const germanPath = await page.inputValue('input[name="AutoroutePart.Path"]');

// Publish it and show the translated page on the site
await page.click('button.publish');
await page.waitForLoadState('networkidle');
await page.goto(`${base}/${germanPath}`);
await page.setViewportSize({ width: 1100, height: 760 });
await page.evaluate(() => window.scrollTo(0, 260));
// JPEG: the theme's photo header would make a PNG ~1 MB.
await page.screenshot({ path: out + 'translated-site.jpg', type: 'jpeg', quality: 70 });
console.log('wrote translated-site.jpg');

// Translating again asks before overwriting
await page.goto(translateUrl);
await page.check('input[value="de-CH"]');
await page.click('button.supertext-submit');
await page.waitForLoadState('networkidle');
await shot(page, 'overwrite-warning.png', '.ta-content', { maxHeight: 520 });

// Orchard Core's own Localizations menu translates the copy automatically
await page.goto(`${base}/Admin/Contents/ContentItems`);
const aboutRow = page.locator('li.list-group-item', { hasText: 'About this demo' });
await aboutRow.locator('button.localizations').click();
await page.waitForTimeout(300);
const rowBox = await aboutRow.boundingBox();
const menuBox = await aboutRow.locator('.dropdown-menu.show').boundingBox();
await page.screenshot({ path: out + 'localizations-menu.png', clip: { x: rowBox.x, y: rowBox.y - 8, width: rowBox.width, height: menuBox.y + menuBox.height - rowBox.y + 16 } });
console.log('wrote localizations-menu.png');
await aboutRow.locator('.dropdown-menu.show a[title="Create German (Switzerland)"]').click();
await page.waitForLoadState('networkidle');
await page.setViewportSize({ width: 1280, height: 900 });
await shot(page, 'localize-translated.png', '.ta-content', { maxHeight: 620 });

await browser.close();
