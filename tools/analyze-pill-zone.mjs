// 第三轮:精确圈定"均匀亮区"连通域 + 锐利度对比(区内的树枝影子是软是硬?)
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
const lum = (x, y) => { const o = y * stride + x * ch; return Math.round(0.299 * px[o] + 0.587 * px[o + 1] + 0.114 * px[o + 2]); };

// --- 1. 8px 块分类:亮区 = mean>195 && std<4 ---
console.log('--- 亮平滑区图(8px 块, X=亮平滑 o=亮纹理 .=暗纹理) ---');
for (let by = 0; by < h; by += 8) {
  let line = '';
  for (let bx = 0; bx < w; bx += 8) {
    let s = 0, s2 = 0, n = 0;
    for (let y = by; y < Math.min(by + 8, h); y++)
      for (let x = bx; x < Math.min(bx + 8, w); x++) { const v = lum(x, y); s += v; s2 += v * v; n++; }
    const m = s / n, std = Math.sqrt(Math.max(0, s2 / n - m * m));
    line += m > 195 ? (std < 4 ? 'X' : 'o') : '.';
  }
  console.log(String(by).padStart(3) + ' ' + line);
}

// --- 2. 锐利度:区内 vs 壁纸的最大水平梯度(5px 窗口) ---
const maxGrad = (x0, x1, y) => {
  let g = 0;
  for (let x = x0; x < x1 - 1; x++) g = Math.max(g, Math.abs(lum(x + 1, y) - lum(x, y)));
  return g;
};
console.log('--- 水平最大梯度对比(树枝影子软硬) ---');
console.log(`区内 y=40 x=505..545: ${maxGrad(505, 545, 40)}  y=75 x=470..545: ${maxGrad(470, 545, 75)}  y=130 x=490..545: ${maxGrad(490, 545, 130)}`);
console.log(`壁纸 y=110 x=300..430: ${maxGrad(300, 430, 110)}  y=130 x=250..430: ${maxGrad(250, 430, 130)}  y=40 x=200..430(胶囊内): ${maxGrad(200, 430, 40)}`);

// --- 3. 区左边界精确扫描(软过渡=模糊面边缘) ---
for (const y of [30, 60, 75, 120, 140]) {
  let s = `y=${String(y).padStart(3)} x=430..545: `;
  for (let x = 430; x <= 545; x += 5) s += String(lum(x, y)).padStart(4);
  console.log(s);
}
// --- 4. 区顶边界(x=500,520 列 y=0..30) ---
for (const x of [490, 520, 545]) {
  let s = `x=${x} y=0..30: `;
  for (let y = 0; y <= 30; y += 2) s += String(lum(x, y)).padStart(4);
  console.log(s);
}
// --- 5. 区底部(x=490..545, y=95..150):是否一直亮到裁剪边缘 ---
for (const x of [470, 500, 540]) {
  let s = `x=${x} y=90..150: `;
  for (let y = 90; y <= 150; y += 4) s += String(lum(x, y)).padStart(4);
  console.log(s);
}
