export function importSpreadsheet(input) {
  return {
    normalizedModel: 'PXA.Spreadsheet.Workbook',
    sheets: [
      {
        name: input.sheet,
        rows: input.rows,
      },
    ],
  };
}
