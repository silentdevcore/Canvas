import { PDFDocument } from 'pdf-lib';
import { fillPdfFormFields, readPdfFormFields, sameFormValue, type PdfFormFieldInfo } from '../features/pdf-viewer/pdfForms';

const createSampleFormPdf = async (): Promise<ArrayBuffer> => {
  const pdf = await PDFDocument.create();
  const page = pdf.addPage([420, 520]);
  const form = pdf.getForm();

  const name = form.createTextField('customer.name');
  name.setText('Ada');
  name.addToPage(page, { x: 40, y: 450, width: 180, height: 24 });

  const notes = form.createTextField('customer.notes');
  notes.enableMultiline();
  notes.setText('Initial note');
  notes.addToPage(page, { x: 40, y: 390, width: 220, height: 46 });

  const approved = form.createCheckBox('approval.accepted');
  approved.check();
  approved.addToPage(page, { x: 40, y: 340, width: 18, height: 18 });

  const priority = form.createDropdown('priority');
  priority.addOptions(['Low', 'Normal', 'High']);
  priority.select('Normal');
  priority.addToPage(page, { x: 40, y: 300, width: 120, height: 24 });

  const channels = form.createOptionList('channels');
  channels.addOptions(['Email', 'Print', 'Archive']);
  channels.select(['Email', 'Archive']);
  channels.addToPage(page, { x: 40, y: 210, width: 130, height: 70 });

  const bytes = await pdf.save();
  const buffer = new ArrayBuffer(bytes.byteLength);
  new Uint8Array(buffer).set(bytes);
  return buffer;
};

describe('pdf viewer form helpers', () => {
  test('reads AcroForm field metadata and values', async () => {
    const fields = await readPdfFormFields(await createSampleFormPdf());

    expect(fields).toEqual(expect.arrayContaining([
      expect.objectContaining({ name: 'customer.name', kind: 'text', value: 'Ada', multiline: false }),
      expect.objectContaining({ name: 'customer.notes', kind: 'text', value: 'Initial note', multiline: true }),
      expect.objectContaining({ name: 'approval.accepted', kind: 'checkbox', value: true }),
      expect.objectContaining({ name: 'priority', kind: 'dropdown', value: 'Normal', options: ['Low', 'Normal', 'High'] }),
      expect.objectContaining({ name: 'channels', kind: 'list', value: ['Email', 'Archive'], options: ['Email', 'Print', 'Archive'] }),
    ]));
  });

  test('fills edited values and can be read again', async () => {
    const sourceBytes = await createSampleFormPdf();
    const fields = await readPdfFormFields(sourceBytes);
    const edited: PdfFormFieldInfo[] = fields.map(field => {
      if (field.name === 'customer.name') {
        return { ...field, value: 'Grace' };
      }

      if (field.name === 'approval.accepted') {
        return { ...field, value: false };
      }

      if (field.name === 'priority') {
        return { ...field, value: 'High' };
      }

      if (field.name === 'channels') {
        return { ...field, value: ['Print'] };
      }

      return field;
    });

    const outputBytes = await fillPdfFormFields(sourceBytes, edited);
    const outputBuffer = new ArrayBuffer(outputBytes.byteLength);
    new Uint8Array(outputBuffer).set(outputBytes);
    const reread = await readPdfFormFields(outputBuffer);

    expect(reread.find(field => field.name === 'customer.name')?.value).toBe('Grace');
    expect(reread.find(field => field.name === 'approval.accepted')?.value).toBe(false);
    expect(reread.find(field => field.name === 'priority')?.value).toBe('High');
    expect(reread.find(field => field.name === 'channels')?.value).toEqual(['Print']);
  });

  test('compares scalar and list values', () => {
    expect(sameFormValue('A', 'A')).toBe(true);
    expect(sameFormValue(true, false)).toBe(false);
    expect(sameFormValue(['A', 'B'], ['A', 'B'])).toBe(true);
    expect(sameFormValue(['B', 'A'], ['A', 'B'])).toBe(false);
  });
});

