import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import userEvent from '@testing-library/user-event';
import { NotificacionesBadge } from './NotificacionesBadge';
import * as notificaciones from '../api/notificaciones';

afterEach(() => vi.restoreAllMocks());

function montar(onClick = vi.fn()) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <NotificacionesBadge onClick={onClick} />
    </QueryClientProvider>,
  );
}

describe('NotificacionesBadge', () => {
  it('muestra el conteo de no leídas', async () => {
    vi.spyOn(notificaciones, 'contarNoLeidas').mockResolvedValue(3);
    montar();

    expect(await screen.findByText('3')).toBeInTheDocument();
  });

  it('dispara onClick al presionar el botón', async () => {
    const onClick = vi.fn();
    vi.spyOn(notificaciones, 'contarNoLeidas').mockResolvedValue(0);
    montar(onClick);

    await userEvent.click(screen.getByLabelText('Notificaciones'));

    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
