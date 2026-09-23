import type { ProductOption } from '../../api/types';
import { IconPlus, IconTrash } from '../../components/Icons';

/**
 * A line being edited. `key` exists only so React can track rows across inserts and
 * removals; it is never sent to the server.
 */
export interface EditableLine {
  key: number;
  productId: number | null;
  quantity: number | null;
}

let nextKey = 1;

export function newLine(): EditableLine {
  return { key: nextKey++, productId: null, quantity: 1 };
}

/**
 * The order's line rows.
 *
 * The Java version maintained a contiguous `items[n]` index across the whole table
 * and renumbered every input after an insert or removal, because Spring's data binder
 * would bind a null element into any gap. That machinery is gone -- the payload is a
 * JSON array -- but two behaviours from it are kept on purpose:
 *
 *   * the table is hidden and replaced by a prompt when there are no rows;
 *   * nothing is computed here. There was no client-side total in BrandX and there is
 *     none now: the unit price is decided server-side when the line is first created,
 *     so the browser does not know what a row is worth until the server says.
 */
export function OrderLineEditor({
  lines,
  products,
  onChange,
  errorFor,
}: {
  lines: EditableLine[];
  products: ProductOption[];
  onChange: (lines: EditableLine[]) => void;
  errorFor: (index: number, field: 'productId' | 'quantity') => string | undefined;
}) {
  const update = (key: number, patch: Partial<EditableLine>) =>
    onChange(lines.map((line) => (line.key === key ? { ...line, ...patch } : line)));

  const removeLine = (key: number) => onChange(lines.filter((line) => line.key !== key));

  return (
    <>
      {lines.length === 0 ? (
        <p className="empty" data-testid="no-lines">
          No line items yet. Add at least one.
        </p>
      ) : (
        <table data-testid="lines">
          <thead>
            <tr>
              <th style={{ width: '55%' }}>Product</th>
              <th style={{ width: '25%' }}>Quantity</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {lines.map((line, index) => (
              <tr className="line-row" key={line.key}>
                <td className={errorFor(index, 'productId') ? 'has-error' : undefined}>
                  <select
                    aria-label={`Product for line ${index + 1}`}
                    value={line.productId ?? ''}
                    onChange={(e) =>
                      update(line.key, {
                        productId: e.currentTarget.value === '' ? null : Number(e.currentTarget.value),
                      })
                    }
                  >
                    <option value="">— select a product —</option>
                    {products.map((product) => (
                      <option key={product.id} value={product.id}>
                        {product.displayName}
                      </option>
                    ))}
                  </select>
                  {errorFor(index, 'productId') && (
                    <p className="field-error">{errorFor(index, 'productId')}</p>
                  )}
                </td>
                <td className={errorFor(index, 'quantity') ? 'has-error' : undefined}>
                  <input
                    type="number"
                    min="1"
                    aria-label={`Quantity for line ${index + 1}`}
                    value={line.quantity ?? ''}
                    onChange={(e) =>
                      update(line.key, {
                        quantity: e.currentTarget.value === '' ? null : Number(e.currentTarget.value),
                      })
                    }
                  />
                  {errorFor(index, 'quantity') && <p className="field-error">{errorFor(index, 'quantity')}</p>}
                </td>
                <td className="actions">
                  <button type="button" className="btn btn-small btn-danger" onClick={() => removeLine(line.key)}>
                    <IconTrash /> Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div style={{ marginTop: 14 }}>
        <button type="button" className="btn" onClick={() => onChange([...lines, newLine()])}>
          <IconPlus /> Add line
        </button>
      </div>
    </>
  );
}
