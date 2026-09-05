// 最终验证:state=3 构建下,胶囊圆角外的像素应与"无应用基线"一致(锐利壁纸,无矩形板材)
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

const A = decode(process.argv[2]); // app running (state=3)
const B = decode(process.argv[3]); // killed baseline
// 截图原点 x0=900,y0=0;胶囊窗口物理 (1100,12)-(1460,99) → 截图 (200,12)-(560,99);圆角半径 29DIP×1.5=43.5px
const X0 = 900;

// 胶囊外采样点(物理坐标):窗口矩形内但圆角外,以及窗口矩形外
const outsidePts = [
  [1108, 20, 'window corner TL (圆角外)'],
  [1452, 20, 'window corner TR (圆角外)'],
  [1108, 92, 'window corner BL (圆角外)'],
  [1452, 92, 'window corner BR (圆角外)'],
  [1475, 55, '窗口右缘外 15px'],
  [1085, 55, '窗口左缘外 15px'],
  [1280, 5, '窗口顶缘外 7px'],
  [1280, 106, '窗口底缘外 7px'],
];
console.log('=== 胶囊圆角外/窗口外:A(应用) vs B(基线) — diff 应≈0(壁纸未被应用改变) ===');
for (const [px, py, name] of outsidePts) {
  const x = px - X0, y = py;
  const av = A.lum(x, y), bv = B.lum(x, y);
  const mark = Math.abs(av - bv) <= 6 ? 'OK' : 'DIFF!';
  console.log(`${mark}  ${name} (${px},${py})  A=${av} B=${bv} diff=${av - bv}`);
}

// 胶囊内采样点:应有差异(应用渲染)
console.log('=== 胶囊内(应用渲染,diff 应大) ===');
for (const [px, py, name] of [[1280, 55, '胶囊中心'], [1180, 55, '球右侧'], [1350, 55, '时间区'], [1250, 20, '胶囊内顶部']]) {
  const x = px - X0, y = py;
  console.log(`   ${name} (${px},${py})  A=${A.lum(x, y)} B=${B.lum(x, y)} diff=${A.lum(x, y) - B.lum(x, y)}`);
}

// 模糊验证:胶囊内局部方差 vs 同位置原始壁纸(B)局部方差
const localVar = (img, cx, cy, r = 10) => {
  let s = 0, s2 = 0, n = 0;
  for (let y = cy - r; y <= cy + r; y++) for (let x = cx - r; x <= cx + r; x++) {
    const v = img.lum(x, y); s += v; s2 += v * v; n++;
  }
  const m = s / n; return Math.sqrt(Math.max(0, s2 / n - m * m));
};
console.log('=== 模糊指纹:胶囊内 std(A,模糊) vs std(B,原壁纸) ===');
for (const [px, py, name] of [[1280, 55, '中心'], [1220, 40, '左上'], [1400, 70, '右下']]) {
  const x = px - X0, y = py;
  console.log(`${name} (${px},${py}): A_std=${localVar(A, x, y).toFixed(1)}  B_std=${localVar(B, x, y).toFixed(1)}  (A 应显著小于 B)`);
}
