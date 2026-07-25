import { describe, it, expect, vi, afterEach } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DetalleAvisoPage } from './DetalleAvisoPage';
import * as avisos from '../api/avisos';
import * as chat from '../api/chat';
import * as orders from '../api/orders';
import { HttpError } from '../api/http';

const estadoAuth = {
  estaAutenticado: true,
  verificado: true,
  usuario: { id: 'comprador', email: '', nombre: 'Comprador', ciudad: '', verificado: false },
};
vi.mock('../auth/AuthContext', () => ({ useAuth: () => estadoAuth }));

afterEach(() => vi.restoreAllMocks());

function montar(id: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/avisos/${id}`]}>
        <Routes>
          <Route path="/avisos/:id" element={<DetalleAvisoPage />} />
          <Route path="/mensajes/:id" element={<div>Conversación abierta</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('DetalleAvisoPage', () => {
  it('muestra el detalle de un aviso', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'a1',
      vendedorId: 'vendedor',
      titulo: 'Bicicleta',
      descripcion: 'Rodado 26, poco uso',
      monto: 800,
      moneda: 'BOB',
      nombreCategoria: 'Deportes',
      nombreCiudad: 'Cochabamba',
      condicion: 'Usado',
      fechaCreacion: '2026-07-18T10:00:00Z',
      fotos: [],
    });
    montar('a1');
    expect(await screen.findByText('Bicicleta')).toBeInTheDocument();
    expect(screen.getByText('Rodado 26, poco uso')).toBeInTheDocument();
  });

  it('muestra "no disponible" ante un 404', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockRejectedValue(
      new HttpError(404, null, 'Petición fallida (404)'),
    );
    montar('inexistente');
    expect(await screen.findByText(/no está disponible/i)).toBeInTheDocument();
  });

  it('contacta al vendedor de un aviso ajeno y navega a la conversación', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'a1',
      vendedorId: 'vendedor',
      titulo: 'Bicicleta',
      descripcion: 'Poco uso',
      monto: 800,
      moneda: 'BOB',
      nombreCategoria: 'Deportes',
      nombreCiudad: 'Cochabamba',
      condicion: 'Usado',
      fechaCreacion: '2026-07-18T10:00:00Z',
      fotos: [],
    });
    vi.spyOn(avisos, 'obtenerMiAviso').mockRejectedValue(new HttpError(403, null, 'No autorizado'));
    vi.spyOn(chat, 'iniciarConversacion').mockResolvedValue({
      id: 'c1',
      avisoId: 'a1',
      compradorId: 'comprador',
      vendedorId: 'vendedor',
      creadaEn: '',
      ultimaActividadEn: '',
      ultimaSecuencia: 0,
      estado: 0,
      origenCierre: null,
      puedeEnviar: true,
    });

    montar('a1');
    fireEvent.click(await screen.findByRole('button', { name: 'Contactar al vendedor' }));

    expect(await screen.findByText('Conversación abierta')).toBeInTheDocument();
  });

  it('no ofrece contactar en un aviso propio', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'a1',
      vendedorId: 'comprador',
      titulo: 'Bicicleta',
      descripcion: 'Poco uso',
      monto: 800,
      moneda: 'BOB',
      nombreCategoria: 'Deportes',
      nombreCiudad: 'Cochabamba',
      condicion: 'Usado',
      fechaCreacion: '2026-07-18T10:00:00Z',
      fotos: [],
    });
    vi.spyOn(avisos, 'obtenerMiAviso').mockResolvedValue({
      id: 'a1',
      vendedorId: 'comprador',
      titulo: 'Bicicleta',
      descripcion: 'Poco uso',
      monto: 800,
      moneda: 'BOB',
      categoriaId: 'cat',
      ciudadId: 'ciu',
      condicion: 'Usado',
      estado: 'Activo',
      estadoModeracion: 'Visible',
      fechaCreacion: '',
      fechaActualizacion: '',
      fotos: [],
    });

    montar('a1');

    expect(await screen.findByText('Bicicleta')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Contactar al vendedor' })).not.toBeInTheDocument();
  });

  it('advierte que proponer compra no paga ni reserva', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'a1',
      vendedorId: 'vendedor',
      titulo: 'Bicicleta',
      descripcion: 'Poco uso',
      monto: 800,
      moneda: 'BOB',
      nombreCategoria: 'Deportes',
      nombreCiudad: 'Cochabamba',
      condicion: 'Usado',
      fechaCreacion: '2026-07-18T10:00:00Z',
      fotos: [],
    });
    vi.spyOn(avisos, 'obtenerMiAviso').mockRejectedValue(new HttpError(403, null, 'No autorizado'));
    vi.spyOn(orders, 'solicitarOrden').mockResolvedValue({
      id: 'o1',
      avisoId: 'a1',
      estado: 'Requested',
      montoAcordado: 800,
      moneda: 'BOB',
      creadaEn: '',
    });
    montar('a1');

    fireEvent.click(await screen.findByRole('button', { name: 'Proponer compra' }));

    expect(screen.getByText(/no realiza ningún pago ni reserva/i)).toBeInTheDocument();
  });
});
