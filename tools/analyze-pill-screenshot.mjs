// 一次性像素取证脚本:解码截图,定位胶囊圆角轮廓与"多余矩形"边界,判断矩形层归属。
// 用法: node tools/analyze-pill-screenshot.mjs <png路径>
import fs from 'node:fs';
import zlib from 'node:zlib';

const file = process.argv[2];
if (!file) { console.error('usage: node analyze-pill-screenshot.mjs <png>'); process.exit(1); }
const buf = fs.readFileSync(file);

// --- PNG 解码(仅支持 8bit RGB/RGBA/灰度,非隔行) ---
let pos = 8;
let w = 0, h = 0, bitDepth = 0, colorType = 0, interlace = 0;
const idat = [];
while (pos < buf.length) {
  const len = buf.readUInt32BE(pos);
  const type = buf.toString('ascii', pos + 4, pos + 8);
  const data = buf.subarray(pos + 8, pos + 8 + len);
  if (type === 'IHDR') {
    w = data.readUInt32BE(0); h = data.readUInt32BE(4);
    bitDepth = data[8]; colorType = data[9]; interlace = data[12];
  } else if (type === 'IDAT') idat.push(data);
  else if (type === 'IEND') break;
  pos += 12 + len;
}
console.log(`PNG ${w}x${h} bit=${bitDepth} colorType=${colorType} interlace=${interlace}`);
if (bitDepth !== 8 || interlace !== 0) { console.error('unsupported png'); process.exit(1); }
const ch = colorType === 6 ? 4 : colorType === 2 ? 3 : colorType === 0 ? 1 : colorType === 4 ? 2 : 0;
if (!ch) { console.error('unsupported colorType'); process.exit(1); }

const raw = zlib.inflateSync(Buffer.concat(idat));
const stride = w * ch;
const px = Buffer.alloc(h * stride);
let rp = 0;
for (let y = 0; y < h; y++) {
  const filter = raw[rp++];
  const line = raw.subarray(rp, rp + stride); rp += stride;
  const out = px.subarray(y * stride, (y + 1) * stride);
  for (let x = 0; x < stride; x++) {
    const a = x >= ch ? out[x - ch] : 0;
    const b = y > 0 ? px[(y - 1) * stride + x] : 0;
    const c = (x >= ch && y > 0) ? px[(y - 1) * stride + x - ch] : 0;
    let v = line[x];
    switch (filter) {
      case 0: break;
      case 1: v = (v + a) & 0xff; break;
      case 2: v = (v + b) & 0xff; break;
      case 3: v = (v + ((a + b) >> 1)) & 0xff; break;
      case 4: {
        const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
        v = (v + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c)) & 0xff; break;
      }
    }
    out[x] = v;
  }
}
const lum = (x, y) => {
  const o = y * stride + x * ch;
  if (colorType === 0 || colorType === 4) return px[o];
  return Math.round(0.299 * px[o] + 0.587 * px[o + 1] + 0.114 * px[o + 2]);
};
const rgb = (x, y) => { const o = y * stride + x * ch; return [px[o], px[o + 1], px[o + 2]]; };

// --- 亮度图采样输出(ASCII,压缩到 ~110x40)---
const cols = 110, rows = Math.round(h / w * cols * 0.5);
const sx = w / cols, sy = h / rows;
const ramp = ' .:-=+*#%@';
let ascii = '';
for (let ry = 0; ry < rows; ry++) {
  for (let rx = 0; rx < cols; rx++) {
    let s = 0, n = 0;
    for (let yy = Math.floor(ry * sy); yy < Math.floor((ry + 1) * sy) && yy < h; yy++)
      for (let xx = Math.floor(rx * sx); xx < Math.floor((rx + 1) * sx) && xx < w; xx++) { s += lum(xx, yy); n++; }
    const v = s / n;
    ascii += ramp[Math.min(9, Math.floor(v / 25.6))];
  }
  ascii += '\n';
}
console.log('--- 亮度图(亮=高) ---\n' + ascii);

// --- 逐行找"亮区"边界:亮度显著高于两侧壁纸的连续区段 ---
// 对每一行输出左右边界(以列号),用于勾勒矩形/胶囊轮廓
const rowEdge = [];
for (let y = 0; y < h; y++) {
  // 平滑行亮度
  const line = []; for (let x = 0; x < w; x++) line.push(lum(x, y));
  // 阈值 = 全图均值
  const mean = line.reduce((s, v) => s + v, 0) / w;
  let l = -1, r = -1;
  for (let x = 0; x < w; x++) if (line[x] > mean + 18) { l = x; break; }
  for (let x = w - 1; x >= 0; x--) if (line[x] > mean + 18) { r = x; break; }
  rowEdge.push([l, r]);
}
// 输出每 6 行的边界
console.log('--- 行亮区边界 (y: left..right, 宽度) ---');
for (let y = 0; y < h; y += 6) {
  const [l, r] = rowEdge[y];
  console.log(`y=${String(y).padStart(3)} ${l < 0 ? '  none' : `${String(l).padStart(4)}..${String(r).padStart(4)} w=${r - l + 1}`}`);
}

// --- 顶部蓝色条检测(日志提到的 #0078D7,判断截图是否含窗口贴顶高亮) ---
let blueCount = 0, blueRows = [];
for (let y = 0; y < Math.min(40, h); y++) {
  let c = 0;
  for (let x = 0; x < w; x++) { const [r, g, b] = rgb(x, y); if (b > 150 && b - r > 60 && b - g > 30) c++; }
  if (c > w * 0.3) { blueCount++; blueRows.push(y); }
}
console.log(`--- 蓝色条行数=${blueCount}${blueRows.length ? ` (y=${blueRows[0]}..${blueRows[blueRows.length - 1]})` : ''} ---`);

// --- 特征点取样 ---
const samples = [];
const pick = (name, x, y) => samples.push(`${name.padEnd(28)} (${x},${y}) rgb=${rgb(x, y).join(',')} lum=${lum(x, y)}`);
// 角落壁纸(左上/右上)
pick('wallpaper-top-left', 8, 8);
pick('wallpaper-top-right', w - 9, 8);
pick('wallpaper-bottom-left', 8, h - 9);
pick('wallpaper-bottom-right', w - 9, h - 9);
console.log('--- 取样 ---\n' + samples.join('\n'));
