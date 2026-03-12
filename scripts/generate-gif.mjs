/**
 * Generates docs/aes-demo.gif — the README demo animation.
 *
 * Usage (from repo root):
 *   cd scripts && npm install && node generate-gif.mjs
 *   (output is written to ../docs/aes-demo.gif)
 */

import { createCanvas } from 'canvas';
import GIFEncoder from 'gif-encoder-2';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// ── Layout constants ───────────────────────────────────────────
const W       = 720;
const H       = 640;   // tall enough for banner + pull output + push output
const LINE_H  = 20;
const PAD_X   = 18;
const PAD_Y   = 18;
const FONT    = '13px "Courier New", monospace';

// ── Palette ────────────────────────────────────────────────────
const BG        = '#0d1117';
const FG        = '#c9d1d9';
const CYAN      = '#56b6c2';
const DARK_GRAY = '#484f58';
const YELLOW    = '#e3b341';
const GREEN     = '#3fb950';
const WHITE     = '#ffffff';
const PICK_BG   = '#1f6e79';

// ── Measure monospace char width once ─────────────────────────
const _mc = createCanvas(20, 20);
const _mx = _mc.getContext('2d');
_mx.font = FONT;
const CW = _mx.measureText('M').width;

// ── Helpers ────────────────────────────────────────────────────
function makeCanvas() {
  const c = createCanvas(W, H);
  const ctx = c.getContext('2d');
  ctx.fillStyle = BG;
  ctx.fillRect(0, 0, W, H);
  ctx.font = FONT;
  ctx.textBaseline = 'top';
  return { c, ctx };
}

function txt(ctx, s, x, y, color = FG) {
  ctx.fillStyle = color;
  ctx.font = FONT;
  ctx.textBaseline = 'top';
  ctx.fillText(s, x, y);
}

const row = n => PAD_Y + n * LINE_H;

// ── Banner ─────────────────────────────────────────────────────
const BANNER = [
  { t: '================================================================', c: DARK_GRAY },
  { t: '    ____  ____  _____', c: CYAN },
  { t: '   / _  || ___||  ___|', c: CYAN },
  { t: '  / /_| ||  _|  \\__ \\', c: CYAN },
  { t: ' /_/  |_||____|/____/  Azure DevOps Excel Sync  v1.0.3', c: CYAN },
  { t: '', c: FG },
  { t: '  /help for commands   /exit to quit', c: DARK_GRAY },
  { t: '================================================================', c: DARK_GRAY },
];

function drawBanner(ctx, n = BANNER.length) {
  for (let i = 0; i < Math.min(n, BANNER.length); i++)
    txt(ctx, BANNER[i].t, PAD_X, row(i), BANNER[i].c);
}

// ── Prompt ─────────────────────────────────────────────────────
const PROMPT_PREFIX = '  AES [MyProject] › ';
const PROMPT_PX = PAD_X + _mx.measureText(PROMPT_PREFIX).width;
const PROMPT_ROW = BANNER.length + 1;   // row 9

function drawPrompt(ctx, r, typed = '', showCursor = true) {
  txt(ctx, '  AES', PAD_X, row(r), CYAN);
  txt(ctx, ' [MyProject]', PAD_X + _mx.measureText('  AES').width, row(r), DARK_GRAY);
  txt(ctx, ' › ', PAD_X + _mx.measureText('  AES [MyProject]').width, row(r), CYAN);
  if (typed) txt(ctx, typed, PROMPT_PX, row(r), WHITE);
  if (showCursor) {
    const tw = typed ? _mx.measureText(typed).width : 0;
    ctx.fillStyle = CYAN;
    ctx.fillRect(PROMPT_PX + tw, row(r), Math.round(CW * 0.85), LINE_H - 4);
  }
}

// ── Picker rows ────────────────────────────────────────────────
function drawPicker(ctx, r, rows) {
  rows.forEach(({ usage, desc, sel }, i) => {
    const y = row(r + i);
    if (sel) {
      ctx.fillStyle = PICK_BG;
      ctx.fillRect(PAD_X, y - 2, W - PAD_X * 2, LINE_H);
      txt(ctx, `  ${usage.padEnd(28)} ${desc}`, PAD_X, y, WHITE);
    } else {
      txt(ctx, `  ${usage.padEnd(28)} ${desc}`, PAD_X, y, DARK_GRAY);
    }
  });
}

// ── GIF encoder ────────────────────────────────────────────────
const OUTPUT = path.join(__dirname, '..', 'docs', 'aes-demo.gif');
fs.mkdirSync(path.dirname(OUTPUT), { recursive: true });

const encoder = new GIFEncoder(W, H, 'neuquant', true);
encoder.setRepeat(0);
encoder.setQuality(10);
encoder.start();

function frame(fn, delay = 80) {
  const { c, ctx } = makeCanvas();
  fn(ctx);
  encoder.setDelay(delay);
  encoder.addFrame(ctx);
}

// ── Scene 1: banner appears line by line ──────────────────────
for (let i = 1; i <= BANNER.length; i++)
  frame(ctx => drawBanner(ctx, i), i === BANNER.length ? 600 : 55);

// ── Scene 2: prompt + typing "/" + picker ─────────────────────
frame(ctx => { drawBanner(ctx); drawPrompt(ctx, PROMPT_ROW); }, 500);

frame(ctx => {
  drawBanner(ctx);
  drawPrompt(ctx, PROMPT_ROW, '/');
  drawPicker(ctx, PROMPT_ROW + 1, [
    { usage: '/pull <id>',        desc: 'Fetch work item hierarchy into Excel',  sel: true  },
    { usage: '/push <file.xlsx>', desc: 'Push Excel edits back to Azure DevOps', sel: false },
    { usage: '/clone <id>',       desc: 'Duplicate work item + children',        sel: false },
  ]);
}, 500);

frame(ctx => {
  drawBanner(ctx);
  drawPrompt(ctx, PROMPT_ROW, '/pu');
  drawPicker(ctx, PROMPT_ROW + 1, [
    { usage: '/pull <id>',        desc: 'Fetch work item hierarchy into Excel',  sel: true  },
    { usage: '/push <file.xlsx>', desc: 'Push Excel edits back to Azure DevOps', sel: false },
  ]);
}, 400);

// down arrow → /push highlighted
frame(ctx => {
  drawBanner(ctx);
  drawPrompt(ctx, PROMPT_ROW, '/pu');
  drawPicker(ctx, PROMPT_ROW + 1, [
    { usage: '/pull <id>',        desc: 'Fetch work item hierarchy into Excel',  sel: false },
    { usage: '/push <file.xlsx>', desc: 'Push Excel edits back to Azure DevOps', sel: true  },
  ]);
}, 300);

// up arrow → /pull highlighted again
frame(ctx => {
  drawBanner(ctx);
  drawPrompt(ctx, PROMPT_ROW, '/pu');
  drawPicker(ctx, PROMPT_ROW + 1, [
    { usage: '/pull <id>',        desc: 'Fetch work item hierarchy into Excel',  sel: true  },
    { usage: '/push <file.xlsx>', desc: 'Push Excel edits back to Azure DevOps', sel: false },
  ]);
}, 300);

// Tab → auto-complete to "/pull "
frame(ctx => { drawBanner(ctx); drawPrompt(ctx, PROMPT_ROW, '/pull '); }, 300);

// type "1234"
for (const t of ['/pull 1', '/pull 12', '/pull 123', '/pull 1234'])
  frame(ctx => { drawBanner(ctx); drawPrompt(ctx, PROMPT_ROW, t); }, t === '/pull 1234' ? 600 : 110);

// ── Scene 3: /pull 1234 output ────────────────────────────────
function drawSubmittedCmd(ctx, r, typed) {
  drawPrompt(ctx, r, typed, false);
}

const PULL_OUT = [
  { t: '  Fetching work items...', c: DARK_GRAY },
  { t: '', c: FG },
  { t: '  ✔  Epic     #1234  Build new feature set', c: GREEN },
  { t: '         ✔  Feature  #1235  Authentication module', c: GREEN },
  { t: '               ✔  Story  #1236  Login page            (3 tasks)', c: GREEN },
  { t: '               ✔  Story  #1237  Token refresh         (2 tasks)', c: GREEN },
  { t: '               ✔  Story  #1238  Logout & session expiry (1 task)', c: GREEN },
  { t: '', c: FG },
  { t: '  Saved → ~/.aes/excel/workitem_1234.xlsx', c: CYAN },
];

for (let n = 1; n <= PULL_OUT.length; n++) {
  frame(ctx => {
    drawBanner(ctx);
    drawSubmittedCmd(ctx, PROMPT_ROW, '/pull 1234');
    for (let i = 0; i < n; i++)
      txt(ctx, PULL_OUT[i].t, PAD_X, row(PROMPT_ROW + 1 + i), PULL_OUT[i].c);
  }, n === PULL_OUT.length ? 1200 : n === 1 ? 700 : 160);
}

// ── Scene 4: second prompt + /push command ────────────────────
// P2 = row after last pull output line + blank gap
const P2 = PROMPT_ROW + 1 + PULL_OUT.length + 1;   // row 21

function drawScene4Base(ctx) {
  drawBanner(ctx);
  drawSubmittedCmd(ctx, PROMPT_ROW, '/pull 1234');
  PULL_OUT.forEach((l, i) => txt(ctx, l.t, PAD_X, row(PROMPT_ROW + 1 + i), l.c));
}

frame(ctx => { drawScene4Base(ctx); drawPrompt(ctx, P2); }, 500);

const PUSH_TYPE = '/push workitem_1234.xlsx';
for (let i = 1; i <= PUSH_TYPE.length; i++) {
  const t = PUSH_TYPE.slice(0, i);
  frame(ctx => { drawScene4Base(ctx); drawPrompt(ctx, P2, t); }, i === PUSH_TYPE.length ? 600 : 50);
}

// ── Scene 5: /push dry-run + confirm + done ───────────────────
const PUSH_OUT = [
  { t: '  ── Dry run ──────────────────────────────────────────', c: DARK_GRAY },
  { t: '  #1236  Title   "Login page"      →  "Login page v2"', c: YELLOW },
  { t: '  #1237  State   Active            →  Closed', c: YELLOW },
  { t: '', c: FG },
  { t: '  Push 2 change(s)?  [y/N]  y', c: WHITE },
  { t: '', c: FG },
  { t: '  ✔  Updated #1236', c: GREEN },
  { t: '  ✔  Updated #1237', c: GREEN },
  { t: '  Done.', c: CYAN },
];

function drawScene5Base(ctx) {
  drawScene4Base(ctx);
  drawSubmittedCmd(ctx, P2, PUSH_TYPE);
}

for (let n = 1; n <= PUSH_OUT.length; n++) {
  frame(ctx => {
    drawScene5Base(ctx);
    for (let i = 0; i < n; i++)
      txt(ctx, PUSH_OUT[i].t, PAD_X, row(P2 + 1 + i), PUSH_OUT[i].c);
  }, n === PUSH_OUT.length ? 4000 : n === 5 ? 800 : 180);
}

// ── Verify layout fits ────────────────────────────────────────
const lastRow = P2 + 1 + PUSH_OUT.length - 1;
const lastY   = PAD_Y + lastRow * LINE_H + LINE_H;
if (lastY > H) console.warn(`WARNING: content at y=${lastY} exceeds canvas height ${H}!`);
else console.log(`Layout OK — last content at y=${lastY}, canvas H=${H}`);

// ── Write output ──────────────────────────────────────────────
encoder.finish();
fs.writeFileSync(OUTPUT, encoder.out.getData());
console.log(`GIF written to ${OUTPUT}  (${(encoder.out.getData().length / 1024).toFixed(0)} KB)`);
