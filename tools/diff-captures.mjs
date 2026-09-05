// A/B 对比:应用运行 vs 杀掉后,同区域像素差异
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
  return { w, h, ch, stride, px, lum: (x, y) => { const o = y * stride + x * ch; return Math.round(0.299 * px[o] + 0.587 * px[o + 1] + 0.114 * px[o + 2]); } };
}

const A = decode(process.argv[2]); // app running
const B = decode(process.argv[3]); // killed
console.log(`A(running) ${A.w}x${A.h}  B(killed) ${B.w}x${B.h}`);

// 差异图:16px 块平均差
console.log('--- 平均亮度差 A-B(16px 块): 实际显示数字=|diff|, -=diff<6(无差异) 坐标原点=截图左上(物理1000,0) ---');
for (let by = 0; by < Math.min(A.h, B.h); by += 16) {
  let line = '';
  for (let bx = 0; bx < Math.min(A.w, B.w); bx += 16) {
    let s = 0, n = 0;
    for (let y = by; y < Math.min(by + 16, A.h, B.h); y += 2)
      for (let x = bx; x < Math.min(bx + 16, A.w, B.w); x += 2) { s += A.lum(x, y) - B.lum(x, y); n++; }
    const d = Math.round(s / n);
    line += String(d).padStart(5);
  }
  console.log(`y=${String(by).padStart(3)} ${line}`);
}

// 特征点对比
const pts = [[470, 75, 'right-of-pill-branch'], [500, 40, 'zone'], [520, 75, 'zone'], [500, 130, 'zone-below'],
  [600, 60, 'zone-right'], [700, 100, 'zone-far'], [800, 60, 'far'], [900, 200, 'far-bottom'],
  [300, 75, 'pill-interior'], [150, 75, 'pill-ball-area'], [500, 5, 'zone-top'], [700, 5, 'top-right'],
  [500, 250, 'below-zone'], [700, 300, 'far-below']];
console.log('--- 特征点 A(running) vs B(killed) ---');
for (const [x, y, name] of pts) {
  console.log(`${name.padEnd(22)} (${x},${y})  A=${String(A.lum(x, y)).padStart(4)}  B=${String(B.lum(x, y)).padStart(4)}  diff=${A.lum(x, y) - B.lum(x, y)}`);
}
