// 第二轮取证:精确边界 + 局部方差(区分"均匀填充层"与"壁纸纹理")
import fs from 'node:fs';
import zlib from 'node:zlib';

const file = process.argv[2];
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
const ch = colorType === 6 ? 4 : colorType === 2 ? 3 : 1;
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
const lum = (x, y) => { const o = y * stride + x * ch;
  if (ch === 1) return px[o];
  if (ch === 2) return px[o];
  return Math.round(0.299 * px[o] + 0.587 * px[o + 1] + 0.114 * px[o + 2]); };
const rgb = (x, y) => { const o = y * stride + x * ch; return [px[o], px[o + 1], px[o + 2]]; };

// --- 1. 水平扫描:胶囊中心行(y=75 附近)与窗口内其他行的亮度剖面 ---
for (const y of [10, 40, 75, 110, 140]) {
  let s = `y=${String(y).padStart(3)}: `;
  for (let x = 0; x < w; x += 10) s += String(lum(x, y)).padStart(4);
  console.log(s);
}
console.log('(每 10px 采样,x=0,10,20...)');

// --- 2. 局部方差图:16x16 块,std<2 视为均匀填充层 ---
console.log('--- 局部方差(16px 块):.=纹理 <3 -=平滑 <8 空格=均匀层 ---');
for (let by = 0; by < h; by += 16) {
  let line = '';
  for (let bx = 0; bx < w; bx += 16) {
    let s = 0, s2 = 0, n = 0;
    for (let y = by; y < Math.min(by + 16, h); y += 2)
      for (let x = bx; x < Math.min(bx + 16, w); x += 2) { const v = lum(x, y); s += v; s2 += v * v; n++; }
    const mean = s / n, std = Math.sqrt(Math.max(0, s2 / n - mean * mean));
    line += std < 1.2 ? ' ' : std < 3 ? '.' : std < 8 ? '-' : '#';
  }
  console.log(line);
}

// --- 3. 精确边缘:在 y=75 行找亮度跳变(±5px 窗口内梯度 > 25) ---
console.log('--- y=75 行跳变点 ---');
let prev = lum(0, 75); const jumps = [];
for (let x = 1; x < w; x++) { const v = lum(x, 75); if (Math.abs(v - prev) > 25) jumps.push(`${x}(${prev}->${v})`); prev = v; }
console.log(jumps.join(' '));
console.log('--- y=20 行跳变点 ---');
prev = lum(0, 20); const jumps2 = [];
for (let x = 1; x < w; x++) { const v = lum(x, 20); if (Math.abs(v - prev) > 25) jumps2.push(`${x}(${prev}->${v})`); prev = v; }
console.log(jumps2.join(' '));
// --- 垂直跳变:x=500(右区)与 x=250(中区) ---
for (const x of [250, 350, 500]) {
  console.log(`--- x=${x} 列跳变点 ---`);
  prev = lum(x, 0); const jj = [];
  for (let y = 1; y < h; y++) { const v = lum(x, y); if (Math.abs(v - prev) > 25) jj.push(`${y}(${prev}->${v})`); prev = v; }
  console.log(jj.join(' '));
}

// --- 4. 关键点 RGB ---
const pts = [[100, 75, 'pill-left-in'], [150, 75, 'pill-left-mid'], [300, 75, 'pill-center'], [430, 75, 'pill-right-in'],
  [470, 75, 'just-right-of-pill'], [500, 40, 'right-zone-top'], [500, 130, 'right-zone-bottom'], [520, 75, 'right-zone-mid'],
  [60, 75, 'left-of-pill'], [30, 75, 'far-left'], [270, 8, 'above-pill-center'], [270, 145, 'below-pill-center'],
  [350, 25, 'upper-mid'], [350, 130, 'lower-mid']];
console.log('--- RGB 取样 ---');
for (const [x, y, name] of pts) console.log(`${name.padEnd(18)} (${x},${y}) rgb=${rgb(x, y).join(',')} lum=${lum(x, y)}`);
