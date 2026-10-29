import {
  acceptsMigrationFile,
  inferMigrationInput,
  migrationFileAccept,
  startsWithZipSignature,
  type MigrationInputContract,
} from '@/features/migrations/sourceFiles';

const report = (extensions: string[], supportsBinary = false): MigrationInputContract => ({
  id: 'report',
  mediaType: 'application/octet-stream',
  fileExtensions: extensions,
  supportsText: true,
  supportsBinary,
});

describe('migration source file contracts', () => {
  test.each([
    ['DevExpress', 'BookingReceipt.Designer.cs', report(['.cs', '.repx'])],
    ['DevExpress', 'BookingReceipt.repx', report(['.cs', '.repx'])],
    ['RDL', 'Monthly_Store_Report.rdl', report(['.rdl', '.rdlc', '.rdlx'], true)],
    ['JasperReports', 'json_Master.jrxml', report(['.jrxml'])],
    ['FastReport', 'invoice.frx', report(['.frx'])],
    ['ActiveReports', 'report.rpx', report(['.rpx'])],
    ['Stimulsoft', 'invoice.mrt', report(['.mrt'])],
    ['Telerik', 'invoice.trdp', report(['.trdx', '.trdp'], true)],
  ])('%s accepts its source file %s', (_provider, fileName, input) => {
    expect(acceptsMigrationFile(fileName, [input])).toBe(true);
  });

  test('does not offer another provider package or a resource file as source', () => {
    const devExpress = report(['.cs', '.repx']);
    expect(acceptsMigrationFile('report.trdp', [devExpress])).toBe(false);
    expect(acceptsMigrationFile('BookingReceipt.resx', [devExpress])).toBe(false);
    expect(migrationFileAccept([devExpress])).toBe('.cs,.repx');
  });

  test('infers Nutrient configuration type from its file extension', () => {
    const inputs: MigrationInputContract[] = [
      { id: 'javascript', mediaType: 'text/javascript', fileExtensions: ['.js', '.mjs'], supportsText: true, supportsBinary: false },
      { id: 'typescript', mediaType: 'text/typescript', fileExtensions: ['.ts', '.tsx'], supportsText: true, supportsBinary: false },
      { id: 'instantJson', mediaType: 'application/json', fileExtensions: ['.json'], supportsText: true, supportsBinary: false },
    ];
    expect(inferMigrationInput('viewer.ts', inputs)?.id).toBe('typescript');
    expect(inferMigrationInput('annotations.json', inputs)?.id).toBe('instantJson');
  });

  test('recognizes packaged reports by their ZIP signature', () => {
    expect(startsWithZipSignature(new Uint8Array([0x50, 0x4b, 0x03, 0x04]))).toBe(true);
    expect(startsWithZipSignature(new Uint8Array([0x3c, 0x52]))).toBe(false);
  });
});
