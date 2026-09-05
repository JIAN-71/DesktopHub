// 胶囊内部背景纹理指标(避开球与时间文字):验证 DwmEnableBlurBehindWindow 模糊仍生效
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
  return { lum: (x, y) => { const o = y * stride + x * ch; return Math.round(0.299 * px[o] + 0.587 * px[o + 1] + 0.114 * px[o + 2]); } };
}
for (const f of process.argv.slice(2)) {
  const img = decode(f);
  let s = 0, s2 = 0, n = 0, g = 0;
  for (let y = 18; y < 40; y++) for (let x = 330; x < 410; x++) {
    const v = img.lum(x, y); s += v; s2 += v * v; n++;
    if (x + 1 < 410) g = Math.max(g, Math.abs(img.lum(x + 1, y) - v));
  }
  const m = s / n;
  console.log(f.split('/').pop(), 'interior mean=' + Math.round(m), 'std=' + Math.sqrt(s2 / n - m * m).toFixed(1), 'maxGrad=' + g);
}
