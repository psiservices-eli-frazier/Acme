import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/client';
import { useFlash } from '../components/Flash';

export interface DeleteRequest {
  /** Exact text of the browser confirm, carried over from the Thymeleaf onsubmit. */
  confirm: string;
  run: () => Promise<void>;
  success: string;
  /** Where to land afterwards. The Java handler redirected to the list either way. */
  listPath: string;
  invalidate: unknown[];
}

/**
 * The delete flow, shared by the list and detail screens.
 *
 * A refused delete (409) is not an error page: it goes back to the list as a banner,
 * because a business-rule refusal belongs on the screen the user was already on. The
 * confirm dialog is a courtesy, not a control -- the server decides.
 */
export function useDeleteAction(): (request: DeleteRequest) => Promise<void> {
  const flash = useFlash();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return useCallback(
    async (request: DeleteRequest) => {
      if (!window.confirm(request.confirm)) return;

      try {
        await request.run();
        flash.setSuccess(request.success);
        await queryClient.invalidateQueries({ queryKey: request.invalidate });
        navigate(request.listPath);
      } catch (error) {
        if (!(error instanceof ApiError)) throw error;

        if (error.status === 409) {
          flash.setError(error.message);
          navigate(request.listPath);
        } else if (error.status === 403) {
          navigate('/access-denied');
        } else if (error.status === 401) {
          navigate('/login');
        } else {
          flash.setError(error.message);
        }
      }
    },
    [flash, queryClient, navigate],
  );
}
