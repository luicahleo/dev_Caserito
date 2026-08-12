import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { contarMensajesNoLeidos } from '../api/chat';
import { getAccessToken } from '../auth/session';
import { crearClienteTiempoReal } from './tiempoReal';

export const claveContadorChat = ['chat-no-leidos'] as const;

export function useContadorChat(activo = true): number {
  const clienteQuery = useQueryClient();
  const consulta = useQuery({
    queryKey: claveContadorChat,
    queryFn: contarMensajesNoLeidos,
    enabled: activo,
    refetchInterval: 120_000,
    refetchOnWindowFocus: true,
  });

  useEffect(() => {
    if (!activo) return;
    const tiempoReal = crearClienteTiempoReal({
      getAccessToken,
      alReconectarGlobal: async () => {
        await clienteQuery.invalidateQueries({ queryKey: claveContadorChat });
      },
    });
    tiempoReal.alActualizarContador(() => {
      void clienteQuery.invalidateQueries({ queryKey: claveContadorChat });
    });
    void tiempoReal.conectar().catch(() => undefined);
    return () => {
      void tiempoReal.detener().catch(() => undefined);
    };
  }, [activo, clienteQuery]);

  return consulta.data ?? 0;
}
