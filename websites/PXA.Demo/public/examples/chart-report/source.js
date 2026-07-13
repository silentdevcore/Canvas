export function createChartReport(input) {
  return {
    documentType: 'ChartReport',
    title: input.title,
    chart: {
      type: 'column',
      series: input.series,
    },
  };
}
