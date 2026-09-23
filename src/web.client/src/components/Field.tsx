import type { ReactNode } from 'react';

/**
 * A labelled form control.
 *
 * The hint is replaced by the error rather than shown alongside it, and the wrapper
 * picks up `has-error` so the control itself turns red -- both as in the Thymeleaf
 * forms.
 */
export function Field({
  label,
  hint,
  error,
  children,
}: {
  label: string;
  hint?: string;
  error?: string;
  children: ReactNode;
}) {
  return (
    <div className={error ? 'field has-error' : 'field'}>
      <label>
        {label}
        {children}
      </label>
      {error ? <p className="field-error">{error}</p> : hint ? <p className="hint">{hint}</p> : null}
    </div>
  );
}

/** A checkbox sits inline with its label rather than under it. */
export function CheckField({
  label,
  hint,
  checked,
  onChange,
}: {
  label: string;
  hint?: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <div className="field">
      <label className="check">
        <input type="checkbox" checked={checked} onChange={(e) => onChange(e.currentTarget.checked)} />
        {label}
      </label>
      {hint && <p className="hint">{hint}</p>}
    </div>
  );
}

export function FieldRow({ children }: { children: ReactNode }) {
  return <div className="field-row">{children}</div>;
}

export function FormActions({ children }: { children: ReactNode }) {
  return <div className="form-actions">{children}</div>;
}
