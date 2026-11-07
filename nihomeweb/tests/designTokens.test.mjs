import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
const css = await readFile(new URL("../src/index.css", import.meta.url), "utf8");
function luminance([h, s, l]) {
  h /= 360;
  s /= 100;
  l /= 100;
  const a = s * Math.min(l, 1 - l);
  const rgb = [0, 8, 4].map((n) => {
    const k = (n + h * 12) % 12;
    const c = l - a * Math.max(-1, Math.min(k - 3, 9 - k, 1));
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  });
  return rgb.reduce((v, c, i) => v + c * [0.2126, 0.7152, 0.0722][i], 0);
}
function token(name) {
  const value = css.match(new RegExp(`--${name}:\\s*([\\d.]+) ([\\d.]+)% ([\\d.]+)%`));
  assert.ok(value, name);
  return value.slice(1).map(Number);
}
const contrast = (a, b) =>
  (Math.max(luminance(a), luminance(b)) + 0.05) / (Math.min(luminance(a), luminance(b)) + 0.05);
test("body, secondary and primary text meet AA on default surfaces", () => {
  for (const foreground of ["foreground", "muted-foreground", "primary"])
    for (const surface of ["background", "card", "muted"])
      assert.ok(contrast(token(foreground), token(surface)) >= 4.5, `${foreground} on ${surface}`);
  assert.ok(
    contrast(token("primary"), token("primary-foreground")) >= 4.5,
    "primary control label",
  );
});
test("shared typography uses solid color and transitions do not overshoot", () => {
  const textRule = css.match(/\.text-gradient-primary\s*\{([^}]+)\}/)?.[1] ?? "";
  assert.doesNotMatch(textRule, /color:\s*transparent|background-clip/);
  assert.doesNotMatch(css, /cubic-bezier\(0\.34, 1\.56/);
});
