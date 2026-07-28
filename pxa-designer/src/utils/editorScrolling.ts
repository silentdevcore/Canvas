export interface HorizontalScroller {
  clientWidth: number;
  scrollWidth: number;
  scrollLeft: number;
}

export function applyVerticalWheelToHorizontalScroll(
  scroller: HorizontalScroller,
  deltaX: number,
  deltaY: number,
): boolean {
  if (
    scroller.scrollWidth <= scroller.clientWidth ||
    Math.abs(deltaX) >= Math.abs(deltaY)
  ) {
    return false;
  }

  const previousScrollLeft = scroller.scrollLeft;
  scroller.scrollLeft += deltaY;
  return scroller.scrollLeft !== previousScrollLeft;
}
