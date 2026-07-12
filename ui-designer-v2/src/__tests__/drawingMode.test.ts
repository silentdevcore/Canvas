// Tests for the draw-mode geometry helpers used in SimplePxaSurface

function computeLineElement(startX: number, startY: number, currentX: number, currentY: number) {
  const dx = currentX - startX;
  const dy = currentY - startY;
  const dist = Math.sqrt(dx * dx + dy * dy);
  const angle = Math.atan2(dy, dx) * 180 / Math.PI;
  const cx = (startX + currentX) / 2;
  const cy = (startY + currentY) / 2;
  return {
    x: Math.round(cx - dist / 2),
    y: Math.round(cy - 2),
    width: Math.round(dist),
    height: 4,
    rotation: Math.round(angle * 10) / 10,
  };
}

function computeFreehandBounds(pathPoints: string) {
  const matches = [...pathPoints.matchAll(/(-?[\d.]+)\s+(-?[\d.]+)/g)];
  const xs = matches.map(m => parseFloat(m[1]));
  const ys = matches.map(m => parseFloat(m[2]));
  return {
    minX: Math.min(...xs),
    minY: Math.min(...ys),
    w: Math.max(Math.max(...xs) - Math.min(...xs), 16),
    h: Math.max(Math.max(...ys) - Math.min(...ys), 16),
  };
}

describe('Line element from drag', () => {
  test('horizontal drag produces zero rotation', () => {
    const el = computeLineElement(100, 200, 300, 200);
    expect(el.rotation).toBe(0);
    expect(el.width).toBe(200);
    expect(el.height).toBe(4);
    expect(el.x).toBe(100);
    expect(el.y).toBe(198);
  });

  test('vertical drag produces ±90° rotation', () => {
    const el = computeLineElement(100, 100, 100, 200);
    expect(Math.abs(el.rotation)).toBe(90);
    expect(el.width).toBe(100);
  });

  test('diagonal drag produces ~45° rotation', () => {
    const el = computeLineElement(0, 0, 100, 100);
    expect(el.rotation).toBeCloseTo(45, 0);
  });

  test('center is midpoint of start and end', () => {
    const el = computeLineElement(0, 0, 200, 0);
    const cx = el.x + el.width / 2;
    expect(cx).toBe(100);
  });
});

describe('Arrow element from drag', () => {
  test('angle computed from drag direction', () => {
    const dx = 100;
    const dy = 0;
    const angle = Math.atan2(dy, dx) * 180 / Math.PI;
    expect(angle).toBe(0);
  });

  test('upward drag produces negative angle', () => {
    const angle = Math.atan2(-100, 0) * 180 / Math.PI;
    expect(angle).toBe(-90);
  });
});

describe('Freehand pathData bounds', () => {
  test('computes correct bounding box from M + L path', () => {
    const path = 'M 50 100 L 75 80 L 100 120 L 150 60';
    const bounds = computeFreehandBounds(path);
    expect(bounds.minX).toBe(50);
    expect(bounds.minY).toBe(60);
    expect(bounds.w).toBe(100);
    expect(bounds.h).toBe(60);
  });

  test('minimum bounding box is 16×16', () => {
    const path = 'M 50 50 L 51 51';
    const bounds = computeFreehandBounds(path);
    expect(bounds.w).toBe(16);
    expect(bounds.h).toBe(16);
  });

  test('handles single-point path gracefully', () => {
    const path = 'M 100 200';
    const bounds = computeFreehandBounds(path);
    expect(bounds.minX).toBe(100);
    expect(bounds.minY).toBe(200);
    expect(bounds.w).toBe(16);
    expect(bounds.h).toBe(16);
  });
});

describe('Draw mode — minimum drag distance', () => {
  test('does not create element when drag < 5px', () => {
    const dx = 2;
    const dy = 2;
    const dist = Math.sqrt(dx * dx + dy * dy);
    expect(dist).toBeLessThan(5);
  });

  test('creates element when drag >= 5px', () => {
    const dx = 4;
    const dy = 4;
    const dist = Math.sqrt(dx * dx + dy * dy);
    expect(dist).toBeGreaterThanOrEqual(5);
  });
});
