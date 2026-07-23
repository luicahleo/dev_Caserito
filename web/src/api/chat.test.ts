import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from './http';
import {
  bloquearParticipante,
  cerrarConversacion,
  crearReporteChat,
  desbloquearParticipante,
  enviarMensaje,
  iniciarConversacion,
  listarConversaciones,
  marcarLectura,
  obtenerMensajes,
  reabrirConversacion,
  type CrearReporteChatRequest,
} from './chat';

describe('api/chat', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('serializa el cierre sin cuerpo ni identificador de usuario objetivo', async () => {
    const spy = vi.spyOn(api, 'PUT').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await cerrarConversacion('conversacion-1');

    expect(spy).toHaveBeenCalledWith('/api/chat/conversaciones/{id}/cierre', {
      params: { path: { id: 'conversacion-1' } },
    });
  });

  it('serializa el bloqueo sin cuerpo ni identificador de usuario objetivo', async () => {
    const spy = vi.spyOn(api, 'PUT').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await bloquearParticipante('conversacion-1');

    expect(spy).toHaveBeenCalledWith('/api/chat/conversaciones/{id}/bloqueo', {
      params: { path: { id: 'conversacion-1' } },
    });
  });

  it('crea reportes sin aceptar ni enviar un usuario objetivo', async () => {
    const spy = vi.spyOn(api, 'POST').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 201 }),
    } as never);
    const reporte: CrearReporteChatRequest = {
      tipoObjetivo: 2,
      mensajeId: null,
      categoria: 1,
      detalle: null,
    };

    await crearReporteChat('conversacion-1', reporte);

    expect(spy).toHaveBeenCalledWith('/api/chat/conversaciones/{id}/reportes', {
      params: { path: { id: 'conversacion-1' } },
      body: reporte,
    });
    expect(spy.mock.calls[0][1]).not.toHaveProperty('body.usuarioObjetivoId');
  });

  it('conserva el estado de envío y convierte las secuencias con Number()', async () => {
    vi.spyOn(api, 'GET').mockResolvedValue({
      data: {
        items: [
          {
            id: 'conversacion-1',
            avisoId: 'aviso-1',
            contraparteId: 'contraparte-1',
            rol: 'Comprador',
            creadaEn: '2026-07-23T10:00:00Z',
            ultimaActividadEn: '2026-07-23T10:01:00Z',
            ultimaSecuencia: '9007199254740991',
            noLeidos: '2',
            estado: 1,
            origenCierre: 'Participante',
            puedeEnviar: false,
          },
        ],
        siguienteCursor: null,
      },
      response: new Response(null, { status: 200 }),
    } as never);

    const pagina = await listarConversaciones();

    expect(pagina.items[0]).toMatchObject({
      ultimaSecuencia: Number('9007199254740991'),
      noLeidos: Number('2'),
      estado: 1,
      origenCierre: 'Participante',
      puedeEnviar: false,
    });
  });

  it('serializa reapertura y desbloqueo como eliminaciones sin cuerpo', async () => {
    const spy = vi.spyOn(api, 'DELETE').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await reabrirConversacion('conversacion-1');
    await desbloquearParticipante('conversacion-1');

    expect(spy).toHaveBeenNthCalledWith(1, '/api/chat/conversaciones/{id}/cierre', {
      params: { path: { id: 'conversacion-1' } },
    });
    expect(spy).toHaveBeenNthCalledWith(2, '/api/chat/conversaciones/{id}/bloqueo', {
      params: { path: { id: 'conversacion-1' } },
    });
  });

  it('inicia una conversación usando únicamente el aviso', async () => {
    const spy = vi.spyOn(api, 'POST').mockResolvedValue({
      data: {
        id: 'c1', avisoId: 'a1', compradorId: 'u1', vendedorId: 'u2',
        creadaEn: '2026-07-23T10:00:00Z', ultimaActividadEn: '2026-07-23T10:00:00Z',
        ultimaSecuencia: '0', estado: 0, origenCierre: null, puedeEnviar: true,
      },
      response: new Response(null, { status: 201 }),
    } as never);

    const conversacion = await iniciarConversacion('a1');

    expect(spy).toHaveBeenCalledWith('/api/chat/conversaciones', { body: { avisoId: 'a1' } });
    expect(conversacion.ultimaSecuencia).toBe(0);
  });

  it('pagina historial, convierte secuencias y conserva el cursor opaco', async () => {
    const spy = vi.spyOn(api, 'GET').mockResolvedValue({
      data: {
        items: [{
          id: 'm1', conversacionId: 'c1', remitenteId: 'u1',
          secuencia: '12', texto: 'Hola', enviadoEn: '2026-07-23T10:00:00Z',
        }],
        siguienteCursor: 'cursor-opaco',
      },
      response: new Response(null, { status: 200 }),
    } as never);

    const pagina = await obtenerMensajes('c1', 'anterior', 25);

    expect(spy).toHaveBeenCalledWith('/api/chat/conversaciones/{id}/mensajes', {
      params: { path: { id: 'c1' }, query: { cursor: 'anterior', limite: 25 } },
    });
    expect(pagina.items[0].secuencia).toBe(12);
    expect(pagina.siguienteCursor).toBe('cursor-opaco');
  });

  it('envía con clave idempotente y marca lectura', async () => {
    const post = vi.spyOn(api, 'POST').mockResolvedValue({
      data: {
        id: 'm1', conversacionId: 'c1', remitenteId: 'u1',
        secuencia: '4', texto: 'Disponible', enviadoEn: '2026-07-23T10:00:00Z',
      },
      response: new Response(null, { status: 201 }),
    } as never);
    const put = vi.spyOn(api, 'PUT').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    const mensaje = await enviarMensaje('c1', 'clave-1', 'Disponible');
    await marcarLectura('c1', 4);

    expect(post).toHaveBeenCalledWith('/api/chat/conversaciones/{id}/mensajes', {
      params: { path: { id: 'c1' } },
      body: { claveIdempotencia: 'clave-1', texto: 'Disponible' },
    });
    expect(mensaje.secuencia).toBe(4);
    expect(put).toHaveBeenCalledWith('/api/chat/conversaciones/{id}/lectura', {
      params: { path: { id: 'c1' } },
      body: { hastaSecuencia: 4 },
    });
  });
});
