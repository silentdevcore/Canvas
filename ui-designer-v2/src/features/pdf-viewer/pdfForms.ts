import {
  PDFCheckBox,
  PDFDocument,
  PDFDropdown,
  PDFOptionList,
  PDFRadioGroup,
  PDFTextField,
} from 'pdf-lib';

export type PdfFormFieldKind = 'text' | 'checkbox' | 'radio' | 'dropdown' | 'list' | 'unsupported';
export type PdfFormFieldValue = string | string[] | boolean;

export interface PdfFormFieldInfo {
  name: string;
  kind: PdfFormFieldKind;
  value: PdfFormFieldValue;
  originalValue: PdfFormFieldValue;
  options: string[];
  multiline?: boolean;
}

interface BackendFormFieldsResponse {
  sourceName: string | null;
  fields: PdfFormFieldInfo[];
}

const cloneFieldValue = (value: PdfFormFieldValue): PdfFormFieldValue => (
  Array.isArray(value) ? [...value] : value
);

export const sameFormValue = (left: PdfFormFieldValue, right: PdfFormFieldValue): boolean => {
  if (Array.isArray(left) || Array.isArray(right)) {
    return Array.isArray(left)
      && Array.isArray(right)
      && left.length === right.length
      && left.every((value, index) => value === right[index]);
  }

  return left === right;
};

export const readPdfFormFields = async (bytes: ArrayBuffer): Promise<PdfFormFieldInfo[]> => {
  const pdf = await PDFDocument.load(bytes);
  const form = pdf.getForm();

  return form.getFields().map(field => {
    const name = field.getName();

    if (field instanceof PDFTextField) {
      const value = field.getText() ?? '';
      return {
        name,
        kind: 'text',
        value,
        originalValue: value,
        options: [],
        multiline: field.isMultiline(),
      };
    }

    if (field instanceof PDFCheckBox) {
      const value = field.isChecked();
      return {
        name,
        kind: 'checkbox',
        value,
        originalValue: value,
        options: [],
      };
    }

    if (field instanceof PDFRadioGroup) {
      const value = field.getSelected() ?? '';
      return {
        name,
        kind: 'radio',
        value,
        originalValue: value,
        options: field.getOptions(),
      };
    }

    if (field instanceof PDFDropdown) {
      const selected = field.getSelected();
      const value = selected[0] ?? '';
      return {
        name,
        kind: 'dropdown',
        value,
        originalValue: value,
        options: field.getOptions(),
      };
    }

    if (field instanceof PDFOptionList) {
      const value = field.getSelected();
      return {
        name,
        kind: 'list',
        value,
        originalValue: cloneFieldValue(value),
        options: field.getOptions(),
      };
    }

    return {
      name,
      kind: 'unsupported',
      value: '',
      originalValue: '',
      options: [],
    };
  });
};

export const extractPdfFormFieldsFromBackend = async (pdfFile: File): Promise<PdfFormFieldInfo[]> => {
  const form = new FormData();
  form.append('file', pdfFile);

  const response = await fetch('/api/pdf-viewer/forms/extract', {
    method: 'POST',
    body: form,
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error || `HTTP ${response.status}`);
  }

  const payload = await response.json() as BackendFormFieldsResponse;
  return payload.fields;
};

export const fillPdfFormFields = async (
  bytes: ArrayBuffer,
  fields: PdfFormFieldInfo[],
  flatten = false,
): Promise<Uint8Array> => {
  const pdf = await PDFDocument.load(bytes);
  const form = pdf.getForm();

  fields.forEach(fieldInfo => {
    if (fieldInfo.kind === 'unsupported') {
      return;
    }

    if (fieldInfo.kind === 'text') {
      form.getTextField(fieldInfo.name).setText(String(fieldInfo.value));
      return;
    }

    if (fieldInfo.kind === 'checkbox') {
      const checkbox = form.getCheckBox(fieldInfo.name);
      if (fieldInfo.value === true) {
        checkbox.check();
      } else {
        checkbox.uncheck();
      }
      return;
    }

    if (fieldInfo.kind === 'radio') {
      const radio = form.getRadioGroup(fieldInfo.name);
      const value = String(fieldInfo.value);
      if (value) {
        radio.select(value);
      } else {
        radio.clear();
      }
      return;
    }

    if (fieldInfo.kind === 'dropdown') {
      const dropdown = form.getDropdown(fieldInfo.name);
      const value = String(fieldInfo.value);
      if (value) {
        dropdown.select(value);
      } else {
        dropdown.clear();
      }
      return;
    }

    if (fieldInfo.kind === 'list') {
      const optionList = form.getOptionList(fieldInfo.name);
      const values = Array.isArray(fieldInfo.value) ? fieldInfo.value : [String(fieldInfo.value)];
      if (values.length > 0) {
        optionList.select(values);
      } else {
        optionList.clear();
      }
    }
  });

  if (flatten) {
    form.flatten();
  } else {
    form.updateFieldAppearances();
  }

  return pdf.save();
};
