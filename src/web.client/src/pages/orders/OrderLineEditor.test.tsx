import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { ProductOption } from '../../api/types';
import { OrderLineEditor, newLine } from './OrderLineEditor';
import type { EditableLine } from './OrderLineEditor';

const PRODUCTS: ProductOption[] = [
  { id: 1, displayName: 'BX-WIDGET-01 - Standard Widget', price: 19.99 },
  { id: 2, displayName: 'BX-WIDGET-02 - Widget Pro', price: 34.5 },
];

function Harness({ initial = [] as EditableLine[] }) {
  const [lines, setLines] = useState<EditableLine[]>(initial);
  return (
    <>
      <OrderLineEditor lines={lines} products={PRODUCTS} onChange={setLines} errorFor={() => undefined} />
      <output data-testid="payload">
        {JSON.stringify(lines.map((l) => ({ productId: l.productId, quantity: l.quantity })))}
      </output>
    </>
  );
}

describe('OrderLineEditor', () => {
  it('shows the prompt and hides the table when there are no lines', () => {
    render(<Harness />);
    expect(screen.getByTestId('no-lines')).toHaveTextContent('No line items yet. Add at least one.');
    expect(screen.queryByTestId('lines')).not.toBeInTheDocument();
  });

  it('adds a row defaulting to quantity 1 and no product', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: /add line/i }));

    expect(screen.getByTestId('lines')).toBeInTheDocument();
    expect(screen.queryByTestId('no-lines')).not.toBeInTheDocument();
    expect(screen.getByTestId('payload')).toHaveTextContent('[{"productId":null,"quantity":1}]');
  });

  it('keeps each row bound to its own values when one in the middle is removed', async () => {
    const user = userEvent.setup();
    render(
      <Harness
        initial={[
          { ...newLine(), productId: 1, quantity: 5 },
          { ...newLine(), productId: 2, quantity: 7 },
          { ...newLine(), productId: 1, quantity: 9 },
        ]}
      />,
    );

    // Remove the middle row. In the Java version this was the case that needed the
    // index renumbering, because a gap bound a null element server-side.
    await user.click(screen.getAllByRole('button', { name: /remove/i })[1]);

    expect(screen.getByTestId('payload')).toHaveTextContent(
      '[{"productId":1,"quantity":5},{"productId":1,"quantity":9}]',
    );
    expect(screen.getByLabelText('Quantity for line 1')).toHaveValue(5);
    expect(screen.getByLabelText('Quantity for line 2')).toHaveValue(9);
  });

  it('returns to the prompt when the last row is removed', async () => {
    const user = userEvent.setup();
    render(<Harness initial={[{ ...newLine(), productId: 1, quantity: 2 }]} />);

    await user.click(screen.getByRole('button', { name: /remove/i }));

    expect(screen.getByTestId('no-lines')).toBeInTheDocument();
    expect(screen.queryByTestId('lines')).not.toBeInTheDocument();
  });

  it('edits a row without disturbing the others', async () => {
    const user = userEvent.setup();
    render(
      <Harness
        initial={[
          { ...newLine(), productId: 1, quantity: 1 },
          { ...newLine(), productId: 2, quantity: 1 },
        ]}
      />,
    );

    await user.selectOptions(screen.getByLabelText('Product for line 1'), '2');
    await user.clear(screen.getByLabelText('Quantity for line 2'));
    await user.type(screen.getByLabelText('Quantity for line 2'), '12');

    expect(screen.getByTestId('payload')).toHaveTextContent(
      '[{"productId":2,"quantity":1},{"productId":2,"quantity":12}]',
    );
  });
});
