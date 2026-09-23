import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';

/**
 * One-shot banner messages.
 *
 * Stands in for the Java `RedirectAttributes` flash scope: a mutation sets a message,
 * the app navigates, the message shows once on the next screen and is dropped on the
 * navigation after that. Refused deletes land here too -- a business-rule refusal
 * belongs on the screen the user was already on, not on an error page.
 */
interface FlashValue {
  success: string | null;
  error: string | null;
  setSuccess: (message: string) => void;
  setError: (message: string) => void;
  clear: () => void;
}

const FlashContext = createContext<FlashValue | null>(null);

export function FlashProvider({ children }: { children: ReactNode }) {
  const [success, setSuccessState] = useState<string | null>(null);
  const [error, setErrorState] = useState<string | null>(null);
  const location = useLocation();

  // A message set during the navigation that is happening right now must survive it;
  // the one after that clears it.
  const survivesNext = useRef(false);

  useEffect(() => {
    if (survivesNext.current) {
      survivesNext.current = false;
      return;
    }
    setSuccessState(null);
    setErrorState(null);
  }, [location.key]);

  const setSuccess = useCallback((message: string) => {
    survivesNext.current = true;
    setErrorState(null);
    setSuccessState(message);
  }, []);

  const setError = useCallback((message: string) => {
    survivesNext.current = true;
    setSuccessState(null);
    setErrorState(message);
  }, []);

  const clear = useCallback(() => {
    setSuccessState(null);
    setErrorState(null);
  }, []);

  const value = useMemo<FlashValue>(
    () => ({ success, error, setSuccess, setError, clear }),
    [success, error, setSuccess, setError, clear],
  );

  return <FlashContext value={value}>{children}</FlashContext>;
}

export function useFlash(): FlashValue {
  const value = useContext(FlashContext);
  if (!value) throw new Error('useFlash must be used inside a FlashProvider');
  return value;
}

export function Alerts() {
  const { success, error } = useFlash();
  return (
    <div>
      {success && <div className="alert alert-ok">{success}</div>}
      {error && <div className="alert alert-bad">{error}</div>}
    </div>
  );
}
