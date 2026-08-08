import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { AuthExternaSimuladorPage } from './AuthExternaSimuladorPage';

describe('AuthExternaSimuladorPage', () => {
  it('muestra escenarios cerrados y conserva únicamente un retorno local', () => {
    render(
      <MemoryRouter
        initialEntries={[
          '/auth/external/simulador?provider=google&returnUrl=https://malicioso.test',
        ]}
      >
        <Routes>
          <Route path="/auth/external/simulador" element={<AuthExternaSimuladorPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByRole('heading', { name: 'Simulador local de Google' })).toBeInTheDocument();
    expect(screen.getAllByRole('radio')).toHaveLength(3);
    const formulario = screen
      .getByRole('button', { name: 'Aprobar acceso simulado' })
      .closest('form');
    expect(formulario).toHaveAttribute('action', '/api/auth/external/simulator/authorize');
    expect(formulario?.querySelector('input[name="returnUrl"]')).toHaveValue('/perfil');
  });
});
