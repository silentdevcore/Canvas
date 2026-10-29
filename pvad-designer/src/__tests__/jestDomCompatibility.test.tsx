/** @jest-environment jsdom */
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

describe('Designer DOM test-tool compatibility', () => {
  test('queries accessible controls through the installed DOM peer', () => {
    render(<section aria-label="Export actions"><button>Export PDF</button></section>);
    const button = screen.getByRole('button', { name: 'Export PDF' });
    expect(button).toBeInTheDocument();
    expect(button).toBeVisible();
    expect(button).toBeEnabled();
    expect(button).toHaveAccessibleName('Export PDF');
    expect(screen.getByRole('region', { name: 'Export actions' })).toContainElement(button);
  });

  test('checks editable form validity and values with React and user-event', async () => {
    const user = userEvent.setup();
    render(<form aria-label="Document metadata">
      <label htmlFor="title">Document title</label>
      <input id="title" name="title" required />
      <label htmlFor="recipient">Recipient email</label>
      <input id="recipient" name="recipient" type="email" defaultValue="invalid" />
    </form>);
    const title = screen.getByRole('textbox', { name: 'Document title' });
    const email = screen.getByRole('textbox', { name: 'Recipient email' });
    expect(title).toBeRequired();
    expect(title).toBeInvalid();
    expect(email).toBeInvalid();
    await user.type(title, 'Synthetic receipt');
    await user.clear(email);
    await user.type(email, 'recipient@example.test');
    expect(title).toHaveValue('Synthetic receipt');
    expect(title).toBeValid();
    expect(email).toBeValid();
    expect(screen.getByRole('form')).toHaveFormValues({
      title: 'Synthetic receipt', recipient: 'recipient@example.test',
    });
  });

  test('distinguishes hidden, absent and disabled controls', () => {
    render(<div>
      <span hidden data-testid="hidden-status">Pending</span>
      <fieldset disabled><button>Publish</button></fieldset>
      <div role="alert">The document could not be saved.</div>
    </div>);
    expect(screen.getByTestId('hidden-status')).toBeInTheDocument();
    expect(screen.getByTestId('hidden-status')).not.toBeVisible();
    expect(screen.queryByText('Saved successfully')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Publish' })).toBeDisabled();
    expect(screen.getByRole('alert')).toHaveTextContent('could not be saved');
  });

  test('rejects incorrect assertions instead of silently accepting them', () => {
    render(<button disabled>Run conversion</button>);
    const button = screen.getByRole('button');
    expect(() => expect(button).toBeEnabled()).toThrow();
    expect(() => expect(button).toHaveAccessibleName('Another action')).toThrow();
    expect(() => expect(null).toBeInTheDocument()).toThrow();
  });

  test('supports v7 query matchers with the required DOM peer', () => {
    render(<section aria-label="Export results">
      <button>Export PDF</button><button>Export JSON</button>
      <label htmlFor="result-title">Document title</label><input id="result-title" />
    </section>);
    const region = screen.getByRole('region', { name: 'Export results' });
    expect(region).toContainAnyByRole('button');
    expect(region).toContainOneByRole('button', { name: 'Export PDF' });
    expect(region).not.toContainOneByRole('button');
    expect(() => expect(region).toContainOneByRole('button')).toThrow();
    expect(region).toContainOneByLabelText('Document title');
    expect(region).not.toContainAnyByRole('alert');
  });
});
