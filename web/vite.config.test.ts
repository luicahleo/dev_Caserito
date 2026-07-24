import { describe, expect, it } from 'vitest';
import type { UserConfig } from 'vite';
import configuracion from './vite.config';

describe('proxy de desarrollo', () => {
  it('reenvía el hub de SignalR con soporte WebSocket', () => {
    const proxy = (configuracion as UserConfig).server?.proxy;

    expect(proxy?.['/hubs']).toMatchObject({ ws: true });
  });
});
