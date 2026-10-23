import { niceTicks } from '@/chart/PdfContractChartRenderer';

describe('PDF contract chart renderer', () => {
  test.each([
    [-7, 31, [-10, 0, 10, 20, 30, 40]],
    [0, 100, [0, 50, 100]],
    [-1, 1, [-1, -0.5, 0, 0.5, 1]],
  ])('uses the backend nice-tick contract for %s to %s', (minimum, maximum, expected) => {
    expect(niceTicks(minimum as number, maximum as number)).toEqual(expected);
  });
});
