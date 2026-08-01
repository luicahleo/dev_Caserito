import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { expect, it, vi } from 'vitest';
import * as ctx from '../auth/AuthContext';
import { AuthExternaCallbackPage } from './AuthExternaCallbackPage';

it('restaura la sesión y descarta un retorno malicioso', async () => {
  const restaurarSesion = vi.fn().mockResolvedValue(undefined);
  vi.spyOn(ctx, 'useAuth').mockReturnValue({ restaurarSesion } as unknown as ReturnType<typeof ctx.useAuth>);
  render(<MemoryRouter initialEntries={['/auth/external/completado?returnUrl=https://malicioso.test']}><Routes>
    <Route path="/auth/external/completado" element={<AuthExternaCallbackPage />} />
    <Route path="/perfil" element={<div>Perfil seguro</div>} />
  </Routes></MemoryRouter>);

  expect(screen.getByText('Completando el acceso…')).toBeInTheDocument();
  await waitFor(() => expect(restaurarSesion).toHaveBeenCalledOnce());
  expect(await screen.findByText('Perfil seguro')).toBeInTheDocument();
});
