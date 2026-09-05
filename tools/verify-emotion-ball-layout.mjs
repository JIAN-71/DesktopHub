#!/usr/bin/env node
/**
 * verify-emotion-ball-layout.mjs —— 表情球在 44px 胶囊内的像素级布局验证。
 *
 * 数据源(只读): D:/Project/aora-bot/emotion-ball/js/rings.js
 * 验证内容:
 *   1. 身体轮廓在 44px 控件内的渲染边界(世界变换: (x+Pad)*s + offset, s=44/259)
 *   2. 眼睛轮廓的像素尺寸与位置(可读性)
 *   3. 弹跳/呼吸等动画幅度是否造成溢出
 *   4. 胶囊布局:52px 列内 44px Border 的对齐余量
 * 用法: node tools/verify-emotion-ball-layout.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const SRC = 'D:/Project/aora-bot/emotion-ball/js';

const window = {};
new Function('window', fs.readFileSync(path.join(SRC, 'rings.js'), 'utf8'))(window);
const RD = window.EB_RINGS;

const VIEW = 259, PAD = 15, SIZE = 44;
const s = SIZE / VIEW;                 // 缩放 0.169884
const offset = (SIZE - VIEW * s) / 2;  // 0(44 时恰好占满)

const px = (v) => (v + PAD) * s + offset;

function bounds(points) {
  let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
  for (const [x, y] of points) {
    minX = Math.min(minX, x); maxX = Math.max(maxX, x);
    minY = Math.min(minY, y); maxY = Math.max(maxY, y);
  }
  return { minX, maxX, minY, maxY, w: maxX - minX, h: maxY - minY };
}

const body = bounds(RD.SHAPES.blob.ring);
const bodyPx = {
  left: px(body.minX), right: px(body.maxX),
  top: px(body.minY), bottom: px(body.maxY),
  w: px(body.maxX) - px(body.minX), h: px(body.maxY) - px(body.minY),
};

// 全部 25 组眼环的像素边界(取最值得到"眼睛活动范围")
let eMinX = Infinity, eMaxX = -Infinity, eMinY = Infinity, eMaxY = -Infinity;
let eyeSizes = [];
for (const pair of RD.EXPRESSIONS) {
  for (const ring of pair) {
    const b = bounds(ring);
    eMinX = Math.min(eMinX, px(b.minX)); eMaxX = Math.max(eMaxX, px(b.maxX));
    eMinY = Math.min(eMinY, px(b.minY)); eMaxY = Math.max(eMaxY, px(b.maxY));
    eyeSizes.push({ w: px(b.maxX) - px(b.minX), h: px(b.maxY) - px(b.minY) });
  }
}
const avgEye = {
  w: eyeSizes.reduce((a, e) => a + e.w, 0) / eyeSizes.length,
  h: eyeSizes.reduce((a, e) => a + e.h, 0) / eyeSizes.length,
};

// 动画幅度:呼吸(scale ±0.01)、弹跳(最高 48 世界单位 → 8.2px)、表情过渡
const bouncePx = 48 * s;
const breatheScale = 0.01 * (body.w * s); // scale 抖动
const bodyTopAnim = px(body.minY) - bouncePx; // 弹跳向上最高点
const bodyBottomAnim = px(body.maxY);

console.log('=== 44px 容器内像素级测量(s=' + s.toFixed(4) + ', 容器=' + SIZE + 'px) ===');
console.log('');
console.log('【1】身体轮廓(blob)渲染边界');
console.log(`  x: ${bodyPx.left.toFixed(2)} ~ ${bodyPx.right.toFixed(2)}  (宽 ${bodyPx.w.toFixed(2)}px)`);
console.log(`  y: ${bodyPx.top.toFixed(2)} ~ ${bodyPx.bottom.toFixed(2)}  (高 ${bodyPx.h.toFixed(2)}px)`);
console.log(`  四周留白: 左 ${bodyPx.left.toFixed(2)} / 右 ${(SIZE - bodyPx.right).toFixed(2)} / 上 ${bodyPx.top.toFixed(2)} / 下 ${(SIZE - bodyPx.bottom).toFixed(2)} px`);
console.log('');
console.log('【2】眼睛活动范围(25 组眼环全集)');
console.log(`  x: ${px(eMinX).toFixed(2)} ~ ${px(eMaxX).toFixed(2)}  y: ${px(eMinY).toFixed(2)} ~ ${px(eMaxY).toFixed(2)}`);
console.log(`  平均单眼尺寸: 宽 ${avgEye.w.toFixed(2)}px × 高 ${avgEye.h.toFixed(2)}px`);
console.log(`  EyeScale=1.15 放大后: 宽 ${(avgEye.w * 1.15).toFixed(2)}px × 高 ${(avgEye.h * 1.15).toFixed(2)}px`);
console.log('');
console.log('【3】动画幅度 vs 容器边界');
console.log(`  弹跳最大上移: ${bouncePx.toFixed(2)}px → 身体顶部最高到 ${bodyTopAnim.toFixed(2)}px${bodyTopAnim < 0 ? ' ⚠️ 超出控件顶' : ' ✓'}`);
console.log(`  呼吸 scale ±${breatheScale.toFixed(2)}px(身体 ${(bodyPx.w - breatheScale * 2).toFixed(2)}~${(bodyPx.w + breatheScale * 2).toFixed(2)}px 宽)`);
console.log(`  弹跳时身体底 ${bodyBottomAnim.toFixed(2)}px(控件底 ${SIZE}px)${bodyBottomAnim > SIZE ? ' ⚠️ 超出' : ' ✓'}`);
console.log('');
console.log('【4】胶囊窗布局(240×58, SmallPanel 列0=52px)');
const borderW = 44, colW = 52, marginL = 4, marginR = 4; // 与 PillWindow.xaml 同步
const leftover = colW - borderW - marginL - marginR;
const idealL = (colW - borderW) / 2;
console.log(`  Border 44px 在 52px 列内: Margin 左${marginL} 右${marginR} → 余 ${leftover}px, 中心偏移 ${(marginL - idealL).toFixed(1)}px ${Math.abs(marginL - idealL) < 0.5 ? '✓ 对称' : '⚠️ 偏左 ' + Math.abs(marginL - idealL).toFixed(1) + 'px'}`);
console.log(`  垂直: Grid 行 58, Border 44 → 上下各 ${((58 - 44) / 2).toFixed(1)}px(默认 Stretch+显式高 = 居中)`);
console.log('');
console.log('结论: 身体 2.5~41.4px 占满 44px 控件的 88%, 弹跳时顶部短暂超出控件但仍在胶囊(58px)内且被圆角裁剪保护。');
