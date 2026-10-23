export function importSpreadsheet(input) {
  return {
    normalizedModel: 'PVAD.Core.Contracts.SpreadsheetDto',
    sheets: [
      {
        name: input.sheet,
        rows: input.rows,
      },
    ],
  };
}
