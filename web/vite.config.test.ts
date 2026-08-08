import { describe, expect, it } from 'vitest';
import type { UserConfig } from 'vite';
import configuracion, { crearHostsPermitidos } from './vite.config';

describe('proxy de desarrollo', () => {
  it('permite únicamente el hostname móvil configurado', () => {
    expect(crearHostsPermitidos('192.168.1.25.sslip.io')).toEqual([
      '192.168.1.25.sslip.io',
    ]);
    expect(crearHostsPermitidos(undefined)).toEqual([]);
  });

  it('reenvía el hub de SignalR con soporte WebSocket', () => {
    const proxy = (configuracion as UserConfig).server?.proxy;

    expect(proxy?.['/hubs']).toMatchObject({ ws: true });
  });
});

describe('ejecución de tests', () => {
  it('limita los workers para evitar timeouts por contención', () => {
    const test = (configuracion as UserConfig & { test?: { maxWorkers?: number } }).test;

    expect(test?.maxWorkers).toBe(2);
  });
});
