import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { HomePage } from './HomePage';

afterEach(() => vi.restoreAllMocks());

describe('HomePage', () => {
  it('muestra el título de CaseritoApp y el estado del backend', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ estado: 'ok' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    const qc = new QueryClient();
    render(
      <QueryClientProvider client={qc}>
        <HomePage />
      </QueryClientProvider>,
    );

    expect(screen.getByText(/CaseritoApp/i)).toBeInTheDocument();
    expect(await screen.findByText(/Backend:/i)).toBeInTheDocument();
  });
});
