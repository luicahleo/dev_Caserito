import { afterEach, describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom';
import { CapturaFlujoNavegacion } from './CapturaFlujoNavegacion';
import { obtenerEventosRecientes } from '../lib/sesionDiagnostico';

afterEach(() => {
  sessionStorage.clear();
});

function BancoDePruebas() {
  return (
    <MemoryRouter initialEntries={['/']}>
      <CapturaFlujoNavegacion />
      <Link to="/avisos/123">ir al aviso</Link>
      <Routes>
        <Route path="/" element={<p>inicio</p>} />
        <Route path="/avisos/:id" element={<p>aviso</p>} />
      </Routes>
    </MemoryRouter>
  );
}

describe('captura de navegación', () => {
  it('registra flow.navigation con rutas sanitizadas en modo diagnóstico', () => {
    sessionStorage.setItem('caserito.debug', '1');
    const antes = obtenerEventosRecientes(100).length;

    render(<BancoDePruebas />);
    fireEvent.click(screen.getByRole('link', { name: 'ir al aviso' }));

    const eventos = obtenerEventosRecientes(100).slice(antes);
    expect(eventos.map((evento) => evento.detail)).toEqual(['/', '/avisos/:id']);
    expect(eventos.every((evento) => evento.eventName === 'flow.navigation')).toBe(true);
  });

  it('registra navegación también sin modo diagnóstico', () => {
    const antes = obtenerEventosRecientes(100).length;

    render(<BancoDePruebas />);
    fireEvent.click(screen.getByRole('link', { name: 'ir al aviso' }));

    const eventos = obtenerEventosRecientes(100).slice(antes);
    expect(eventos.map((evento) => evento.detail)).toEqual(['/', '/avisos/:id']);
  });
});
