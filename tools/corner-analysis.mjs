// 角落取证:窗口矩形(1100..1460 × 12..99 物理)四个圆角帽之外的差集区域,
// 对比"同帧内相邻壁纸"的结构统计 —— 运行态若角落被模糊(矩形 surface 露出),
// 则 std(corner) ≪ std(相邻壁纸);若干净则两者相当。杀掉后的基线作对照。
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
  return { w, h, stride, ch, px,
    lum: (x, y) => { const o = y * stride + x * ch; return Math.round(0.299 * px[o] + 0.587 * px[o + 1] + 0.114 * px[o + 2]); } };
}

const X0 = 1000;
const stats = (img, x0, y0, x1, y1) => {
  let s = 0, s2 = 0, n = 0, g = 0;
  for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
    const v = img.lum(x, y); s += v; s2 += v * v; n++;
    if (x + 1 < x1) g = Math.max(g, Math.abs(img.lum(x + 1, y) - v));
  }
  const m = s / n;
  return { mean: Math.round(m), std: +Math.sqrt(Math.max(0, s2 / n - m * m)).toFixed(1), maxGrad: g };
};

// 胶囊窗口物理 (1100,12)-(1460,99),圆角半径 29DIP×1.5=43.5px,圆心 (1143.5,55.5)/(1416.5,55.5)
// 角落差集采样(窗口内、圆角外):每角取圆心正交方向 30×30 区域
const corners = [
  { name: 'TL', x0: 1102, y0: 14, x1: 1132, y1: 44 },   // 左上帽外
  { name: 'TR', x0: 1430, y0: 14, x1: 1458, y1: 44 },   // 右上帽外
  { name: 'BL', x0: 1102, y0: 68, x1: 1132, y1: 97 },   // 左下帽外
  { name: 'BR', x0: 1430, y0: 68, x1: 1458, y1: 97 },   // 右下帽外
  // 对照:窗口外紧邻壁纸(同帧,免壁纸运动干扰)
  { name: 'wall-left', x0: 1040, y0: 30, x1: 1070, y1: 60 },
  { name: 'wall-right', x0: 1490, y0: 30, x1: 1520, y1: 60 },
  { name: 'wall-below', x0: 1200, y0: 110, x1: 1230, y1: 140 },
];

for (const file of process.argv.slice(2)) {
  const img = decode(file);
  console.log(`\n=== ${file} ===`);
  for (const c of corners) {
    const s = stats(img, c.x0 - X0, c.y0, c.x1 - X0, c.y1);
    console.log(`${c.name.padEnd(10)} mean=${String(s.mean).padStart(4)}  std=${String(s.std).padStart(6)}  maxGrad=${s.maxGrad}`);
  }
}
