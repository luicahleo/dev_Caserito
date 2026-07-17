import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import * as auth from '../api/auth';
import { obtenerPerfil, type Perfil } from '../api/perfil';
import { getAccessToken } from './session';
import { decodificarClaims } from './jwt';

interface EstadoAuth {
  usuario: Perfil | null;
  estaAutenticado: boolean;
  cargando: boolean;
  permisos: string[];
  verificado: boolean;
  tienePermiso: (p: string) => boolean;
  iniciarSesion: (cred: auth.Credenciales) => Promise<void>;
  registrar: (datos: auth.RegistroDatos) => Promise<void>;
  cerrarSesion: () => Promise<void>;
}

const AuthContext = createContext<EstadoAuth | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Perfil | null>(null);
  const [cargando, setCargando] = useState(true);
  const [claims, setClaims] = useState(() => decodificarClaims(getAccessToken()));
  const sincronizarClaims = useCallback(() => {
    setClaims(decodificarClaims(getAccessToken()));
  }, []);

  useEffect(() => {
    let activo = true;
    (async () => {
      if (await auth.refrescar()) {
        try {
          const p = await obtenerPerfil();
          if (activo) setUsuario(p);
        } catch {
          if (activo) setUsuario(null);
        }
      }
      if (activo) sincronizarClaims();
      if (activo) setCargando(false);
    })();
    return () => {
      activo = false;
    };
  }, [sincronizarClaims]);

  const iniciarSesion = useCallback(
    async (cred: auth.Credenciales) => {
      await auth.iniciarSesion(cred);
      setUsuario(await obtenerPerfil());
      sincronizarClaims();
    },
    [sincronizarClaims],
  );

  const registrar = useCallback(
    async (datos: auth.RegistroDatos) => {
      await auth.registrar(datos);
      await auth.iniciarSesion({ email: datos.email, password: datos.password });
      setUsuario(await obtenerPerfil());
      sincronizarClaims();
    },
    [sincronizarClaims],
  );

  const cerrarSesion = useCallback(async () => {
    await auth.cerrarSesion();
    setUsuario(null);
    setClaims(decodificarClaims(null));
  }, []);

  return (
    <AuthContext.Provider
      value={{
        usuario,
        estaAutenticado: usuario !== null,
        cargando,
        permisos: claims.permisos,
        verificado: claims.verificado,
        tienePermiso: (p: string) => claims.permisos.includes(p),
        iniciarSesion,
        registrar,
        cerrarSesion,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): EstadoAuth {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}
