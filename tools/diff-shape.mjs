// 精确圈出应用的渲染足迹:|A-B| 差异图 + 蓝色条检测(A 图)
import fs from 'node:fs';
import zlib from 'node:zlib';

function decode(file) {
  const buf = fs.readFileSync(file);
  let pos = 8, w = 0, h = 0, colorType = 0;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('ascii', pos + 4, pos + 8);
    const data = buf.subarray(pos + 8, pos + 8 + len);
    if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); colorType = data[9]; }
    else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  const ch = colorType === 6 ? 4 : 3;
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
        case 1: v = (v + a) & 0xff; break;
        case 2: v = (v + b) & 0xff; break;
        case 3: v = (v + ((a + b) >> 1)) & 0xff; break;
        case 4: { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
          v = (v + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c)) & 0xff; break; }
      }
      out[x] = v;
    }
  }
  return { w, h, ch, stride, px,
    lum: (x, y) => { const o = y * stride + x * ch; return Math.round(0.299 * px[o] + 0.587 * px[o + 1] + 0.114 * px[o + 2]); },
    rgb: (x, y) => { const o = y * stride + x * ch; return [px[o], px[o + 1], px[o + 2]]; } };
}

const A = decode(process.argv[2]);
const B = decode(process.argv[3]);

// --- 足迹图:8px 块,|avg diff|>10 记为应用渲染 ---
console.log('--- 应用渲染足迹(8px 块, #:差异>25, +:10..25, .:<10) 坐标=物理-1000 ---');
const W = Math.min(A.w, B.w), H = Math.min(A.h, B.h);
let minX = 1e9, maxX = -1, minY = 1e9, maxY = -1;
for (let by = 0; by < H; by += 8) {
  let line = '';
  for (let bx = 0; bx < W; bx += 8) {
    let s = 0, n = 0;
    for (let y = by; y < Math.min(by + 8, H); y += 2)
      for (let x = bx; x < Math.min(bx + 8, W); x += 2) { s += Math.abs(A.lum(x, y) - B.lum(x, y)); n++; }
    const d = s / n;
    if (d > 10) { if (bx < minX) minX = bx; if (bx > maxX) maxX = bx; if (by < minY) minY = by; if (by > maxY) maxY = by; }
    line += d > 25 ? '#' : d > 10 ? '+' : '.';
  }
  console.log(`y=${String(by).padStart(3)} ${line}`);
}
console.log(`足迹边界: x=[${1000 + minX}, ${1000 + maxX}] y=[${minY}, ${maxY}]`);

// --- 蓝色条检测(A 图):B-R>40 且 B>120 ---
console.log('--- A 图蓝色像素行分布(B-R>40 && B>120) ---');
for (let y = 0; y < Math.min(30, A.h); y++) {
  let cnt = 0, l = -1, r = -1;
  for (let x = 0; x < A.w; x++) {
    const [R, G, Bl] = A.rgb(x, y);
    if (Bl - R > 40 && Bl > 120) { cnt++; if (l < 0) l = x; r = x; }
  }
  if (cnt > 10) console.log(`y=${y}: count=${cnt} x=[${l},${r}] sample=${A.rgb(Math.round((l + r) / 2), y).join(',')}`);
}
// B 图对照
console.log('--- B 图蓝色像素行分布 ---');
let bAny = false;
for (let y = 0; y < Math.min(30, B.h); y++) {
  let cnt = 0;
  for (let x = 0; x < B.w; x++) { const [R, G, Bl] = B.rgb(x, y); if (Bl - R > 40 && Bl > 120) cnt++; }
  if (cnt > 10) { bAny = true; console.log(`y=${y}: count=${cnt}`); }
}
if (!bAny) console.log('(无)');

// --- 足迹的列/行剖面(块 avg diff) ---
console.log('--- 足迹列剖面(y=12..99 平均 diff,每 8px) ---');
let prof = '';
for (let bx = 0; bx < W; bx += 8) {
  let s = 0, n = 0;
  for (let y = 12; y < 99; y += 2) for (let x = bx; x < Math.min(bx + 8, W); x += 2) { s += Math.abs(A.lum(x, y) - B.lum(x, y)); n++; }
  prof += String(Math.round(s / n)).padStart(5);
}
console.log(prof);
console.log('--- 足迹行剖面(x=1100..1460 平均 diff,每 8px) ---');
prof = '';
for (let by = 0; by < Math.min(H, 130); by += 8) {
  let s = 0, n = 0;
  for (let x = 100; x < 460; x += 2) for (let y = by; y < Math.min(by + 8, H); y += 2) { s += Math.abs(A.lum(x, y) - B.lum(x, y)); n++; }
  prof += String(Math.round(s / n)).padStart(5);
}
console.log(prof);
