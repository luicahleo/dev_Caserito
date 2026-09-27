# UI web de la conversación retenida — Plan de implementación

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usar
> `superpowers:subagent-driven-development` (recomendado) o
> `superpowers:executing-plans` para implementar tarea por tarea. Los pasos usan
> casillas (`- [ ]`) para seguimiento.

**Spec:** `docs/superpowers/specs/2026-09-24-chat-conversacion-retenida-web-design.md`

**Objetivo:** Que el comprador con una conversación retenida entienda que su
mensaje está guardado pero no entregado, sepa cómo verificarse y pueda liberar
la conversación cuando ya lo hizo.

**Arquitectura:** Un módulo nuevo centraliza los valores del enum
`EstadoConversacion`, que viaja como número y hoy está replicado como literales
en dos páginas. Un componente nuevo encapsula el aviso de retención, su CTA y el
reintento de liberación. Las dos páginas de chat consumen ambos.

**Stack:** React 19 + TypeScript estricto, MUI, React Router, TanStack Query,
Vitest + Testing Library. Todos los comandos de frontend se ejecutan desde
`web/`; el gate de calidad, desde la raíz del repositorio.

## Restricciones globales

- Trabajar en la rama `develop`. No crear ramas. No hacer push.
- Textos de UI y comentarios en español, con acentos y UTF-8.
- Anti-PII: ningún texto de mensaje, id técnico ni dato de KYC en la UI, en los
  errores ni en la consola. Errores genéricos.
- No modificar backend, contrato OpenAPI ni `web/src/api/schema.d.ts`.
- No modificar `web/src/chat/estadoMensaje.ts` ni
  `web/src/chat/EstadoEntregaMensaje.tsx`.
- Antes de cada commit, desde la raíz: `./verify.ps1 -Changed`. Prohibido
  `--no-verify`. Si el gate falla, se arregla el código, nunca el gate.
- Valores del enum, fijados por `CaseritoApp/src/.../Conversaciones/EstadoConversacion.cs`:
  `Activa = 0`, `Cerrada = 1`, `CerradaPorModeracion = 2`,
  `RetenidaPorVerificacion = 3`.
- Copy exacto, sin variaciones:
  - Aviso: `Tu mensaje está guardado, pero todavía no se ha entregado. Para que llegue, verifica tu identidad.`
  - CTA: `Verificar mi identidad` (enlace a `/kyc`)
  - Reintento: `Ya verifiqué mi identidad`
  - Aún retenida: `Tu verificación todavía no está aprobada.`
  - Error: `No se pudo comprobar tu verificación. Inténtalo nuevamente.`
  - Chip del listado: `En espera de verificación`

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `web/src/chat/estadoConversacion.ts` (crear) | Valores del enum y helpers de lectura |
| `web/src/chat/estadoConversacion.test.ts` (crear) | Test de contrato de los valores |
| `web/src/chat/AvisoConversacionRetenida.tsx` (crear) | Aviso, CTA y reintento de liberación |
| `web/src/chat/AvisoConversacionRetenida.test.tsx` (crear) | Los cuatro casos del aviso |
| `web/src/routes/ConversacionesPage.tsx` (modificar) | Consume `etiquetaEstado` |
| `web/src/routes/ConversacionesPage.test.tsx` (modificar) | Caso del chip retenido |
| `web/src/routes/ConversacionPage.tsx` (modificar) | Monta el aviso, migra literales |
| `web/src/routes/ConversacionPage.test.tsx` (modificar) | Caso de conversación retenida |

---

### Tarea 1: Módulo central de `EstadoConversacion`

**Archivos:**
- Crear: `web/src/chat/estadoConversacion.ts`
- Test: `web/src/chat/estadoConversacion.test.ts`

**Interfaces:**
- Consume: nada.
- Produce:
  - `EstadoConversacion: { readonly Activa: 0; readonly Cerrada: 1; readonly CerradaPorModeracion: 2; readonly RetenidaPorVerificacion: 3 }`
  - `esRetenida(estado: number): boolean`
  - `etiquetaEstado(conversacion: { estado: number; puedeEnviar: boolean }): string`

- [ ] **Paso 1: Escribir el test que falla**

Crear `web/src/chat/estadoConversacion.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { EstadoConversacion, esRetenida, etiquetaEstado } from './estadoConversacion';

describe('EstadoConversacion', () => {
  // El contrato expone el estado como número. Estos valores replican
  // CaseritoApp.Chat.Domain.Conversaciones.EstadoConversacion: si alguien
  // reordena el enum del backend, este test falla en lugar de dejar que la UI
  // muestre el estado equivocado en silencio.
  it('fija los valores numéricos del enum del backend', () => {
    expect(EstadoConversacion.Activa).toBe(0);
    expect(EstadoConversacion.Cerrada).toBe(1);
    expect(EstadoConversacion.CerradaPorModeracion).toBe(2);
    expect(EstadoConversacion.RetenidaPorVerificacion).toBe(3);
  });

  it('reconoce solo el estado retenido', () => {
    expect(esRetenida(EstadoConversacion.RetenidaPorVerificacion)).toBe(true);
    expect(esRetenida(EstadoConversacion.Activa)).toBe(false);
    expect(esRetenida(EstadoConversacion.Cerrada)).toBe(false);
    expect(esRetenida(EstadoConversacion.CerradaPorModeracion)).toBe(false);
  });

  it('etiqueta cada estado', () => {
    expect(etiquetaEstado({ estado: 2, puedeEnviar: false })).toBe('Cerrada por moderación');
    expect(etiquetaEstado({ estado: 1, puedeEnviar: false })).toBe('Cerrada');
    expect(etiquetaEstado({ estado: 3, puedeEnviar: true })).toBe('En espera de verificación');
    expect(etiquetaEstado({ estado: 0, puedeEnviar: false })).toBe('Envío no disponible');
    expect(etiquetaEstado({ estado: 0, puedeEnviar: true })).toBe('Activa');
  });
});
```

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `web/`: `npx vitest run src/chat/estadoConversacion.test.ts`

Esperado: FAIL — no se resuelve el módulo `./estadoConversacion`.

- [ ] **Paso 3: Implementación mínima**

Crear `web/src/chat/estadoConversacion.ts`:

```ts
// Refleja CaseritoApp.Chat.Domain.Conversaciones.EstadoConversacion, que EF y el
// serializador exponen como int. El contrato TypeScript solo declara `number`,
// así que este módulo es el único sitio donde vive la correspondencia.
export const EstadoConversacion = {
  Activa: 0,
  Cerrada: 1,
  CerradaPorModeracion: 2,
  RetenidaPorVerificacion: 3,
} as const;

export function esRetenida(estado: number): boolean {
  return estado === EstadoConversacion.RetenidaPorVerificacion;
}

export function etiquetaEstado(conversacion: { estado: number; puedeEnviar: boolean }): string {
  if (conversacion.estado === EstadoConversacion.CerradaPorModeracion) {
    return 'Cerrada por moderación';
  }
  if (conversacion.estado === EstadoConversacion.Cerrada) return 'Cerrada';
  if (esRetenida(conversacion.estado)) return 'En espera de verificación';
  if (!conversacion.puedeEnviar) return 'Envío no disponible';
  return 'Activa';
}
```

- [ ] **Paso 4: Ejecutar el test y verificar que pasa**

Desde `web/`: `npx vitest run src/chat/estadoConversacion.test.ts`

Esperado: PASS, 3 tests.

- [ ] **Paso 5: Commit**

Desde la raíz del repositorio:

```bash
./verify.ps1 -Changed
git add web/src/chat/estadoConversacion.ts web/src/chat/estadoConversacion.test.ts
git commit -m "feat(chat): centraliza los valores de EstadoConversacion en la web"
```

---

### Tarea 2: El listado muestra la conversación retenida

**Archivos:**
- Modificar: `web/src/routes/ConversacionesPage.tsx` (función `etiquetaEstado`,
  líneas 20-25, y sus importaciones)
- Test: `web/src/routes/ConversacionesPage.test.tsx`

**Interfaces:**
- Consume: `etiquetaEstado` de `web/src/chat/estadoConversacion.ts` (Tarea 1).
- Produce: nada para tareas posteriores.

- [ ] **Paso 1: Escribir el test que falla**

Añadir a `web/src/routes/ConversacionesPage.test.tsx`, dentro del
`describe('ConversacionesPage', ...)` existente:

```tsx
  it('muestra el estado de una conversación en espera de verificación', async () => {
    vi.spyOn(chat, 'listarConversaciones').mockResolvedValue({
      siguienteCursor: null,
      items: [
        {
          id: 'id-tecnico-conversacion',
          avisoId: 'id-tecnico-aviso',
          contraparteId: 'id-tecnico-persona',
          rol: 'Comprador',
          creadaEn: '2026-07-23T10:00:00Z',
          ultimaActividadEn: '2026-07-23T10:01:00Z',
          ultimaSecuencia: 1,
          noLeidos: 0,
          estado: 3,
          origenCierre: null,
          puedeEnviar: true,
        },
      ],
    });
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'id-tecnico-aviso',
      vendedorId: 'id-vendedor',
      titulo: 'Mesa de madera',
      descripcion: '',
      monto: 10,
      moneda: 'BOB',
      nombreCategoria: '',
      nombreCiudad: '',
      condicion: 'Usado',
      fechaCreacion: '',
      fotos: [],
    });

    montar();

    expect(await screen.findByText('En espera de verificación')).toBeInTheDocument();
  });
```

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `web/`: `npx vitest run src/routes/ConversacionesPage.test.tsx`

Esperado: FAIL en el test nuevo — aparece «Activa» en vez de «En espera de
verificación». Los dos tests preexistentes siguen en verde.

- [ ] **Paso 3: Implementación mínima**

En `web/src/routes/ConversacionesPage.tsx`, borrar la función local:

```tsx
function etiquetaEstado(conversacion: ConversacionResumen) {
  if (conversacion.estado === 2) return 'Cerrada por moderación';
  if (conversacion.estado === 1) return 'Cerrada';
  if (!conversacion.puedeEnviar) return 'Envío no disponible';
  return 'Activa';
}
```

y añadir la importación:

```tsx
import { etiquetaEstado } from '../chat/estadoConversacion';
```

El uso en el `Chip` no cambia. Si `ConversacionResumen` deja de usarse en el
archivo, quitarlo de su importación para no romper el lint.

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `web/`:
- `npx vitest run src/routes/ConversacionesPage.test.tsx` → PASS, 3 tests.
- `npm run typecheck` → sin errores.
- `npm run lint` → sin errores.

- [ ] **Paso 5: Commit**

Desde la raíz del repositorio:

```bash
./verify.ps1 -Changed
git add web/src/routes/ConversacionesPage.tsx web/src/routes/ConversacionesPage.test.tsx
git commit -m "feat(chat): etiqueta la conversación retenida en el listado"
```

---

### Tarea 3: Componente del aviso de retención

**Archivos:**
- Crear: `web/src/chat/AvisoConversacionRetenida.tsx`
- Test: `web/src/chat/AvisoConversacionRetenida.test.tsx`

**Interfaces:**
- Consume: `esRetenida` de `web/src/chat/estadoConversacion.ts` (Tarea 1);
  `iniciarConversacion(avisoId: string): Promise<Conversacion>` de
  `web/src/api/chat.ts`.
- Produce: `AvisoConversacionRetenida(props: { avisoId: string; alLiberar: () => void }): JSX.Element`.

**Nota de diseño:** el componente usa `useState` y llama a `iniciarConversacion`
directamente, **no** `useMutation`. `useMutation` exigiría un
`QueryClientProvider` en el test y rompería la frontera fijada en §5 del spec:
el componente no conoce el `QueryClient`; la invalidación es de la página.

- [ ] **Paso 1: Escribir el test que falla**

Crear `web/src/chat/AvisoConversacionRetenida.test.tsx`:

```tsx
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AvisoConversacionRetenida } from './AvisoConversacionRetenida';
import * as chat from '../api/chat';

afterEach(() => vi.restoreAllMocks());

// iniciarConversacion devuelve `Conversacion` (ConversacionDto): lleva
// compradorId y vendedorId, no contraparteId ni rol. No confundir con
// `ConversacionResumen`, que es lo que devuelve buscarConversacionPropia.
const conversacionLiberada: chat.Conversacion = {
  id: 'c1',
  avisoId: 'a1',
  compradorId: 'yo',
  vendedorId: 'otra-persona',
  creadaEn: '',
  ultimaActividadEn: '',
  ultimaSecuencia: 1,
  estado: 0,
  origenCierre: null,
  puedeEnviar: true,
  ultimaSecuenciaEntregadaContraparte: 0,
  ultimaSecuenciaLeidaContraparte: 0,
};

const conversacionRetenida: chat.Conversacion = { ...conversacionLiberada, estado: 3 };

function montar(alLiberar = vi.fn()) {
  render(
    <MemoryRouter>
      <AvisoConversacionRetenida avisoId="a1" alLiberar={alLiberar} />
    </MemoryRouter>,
  );
  return alLiberar;
}

describe('AvisoConversacionRetenida', () => {
  it('explica la retención y enlaza a la verificación', () => {
    montar();

    expect(
      screen.getByText(/todavía no se ha entregado/),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Verificar mi identidad' })).toHaveAttribute(
      'href',
      '/kyc',
    );
  });

  it('libera la conversación cuando la verificación ya está aprobada', async () => {
    const iniciar = vi
      .spyOn(chat, 'iniciarConversacion')
      .mockResolvedValue(conversacionLiberada);
    const alLiberar = montar();

    await userEvent.click(screen.getByRole('button', { name: 'Ya verifiqué mi identidad' }));

    expect(iniciar).toHaveBeenCalledWith('a1');
    expect(alLiberar).toHaveBeenCalledTimes(1);
  });

  it('informa sin error cuando la verificación sigue sin aprobarse', async () => {
    vi.spyOn(chat, 'iniciarConversacion').mockResolvedValue(conversacionRetenida);
    const alLiberar = montar();

    await userEvent.click(screen.getByRole('button', { name: 'Ya verifiqué mi identidad' }));

    expect(
      await screen.findByText('Tu verificación todavía no está aprobada.'),
    ).toBeInTheDocument();
    expect(alLiberar).not.toHaveBeenCalled();
  });

  it('muestra un error genérico cuando la comprobación falla', async () => {
    vi.spyOn(chat, 'iniciarConversacion').mockRejectedValue(new Error('fallo'));
    const alLiberar = montar();

    await userEvent.click(screen.getByRole('button', { name: 'Ya verifiqué mi identidad' }));

    expect(
      await screen.findByText('No se pudo comprobar tu verificación. Inténtalo nuevamente.'),
    ).toBeInTheDocument();
    expect(alLiberar).not.toHaveBeenCalled();
  });
});
```

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `web/`: `npx vitest run src/chat/AvisoConversacionRetenida.test.tsx`

Esperado: FAIL — no se resuelve el módulo `./AvisoConversacionRetenida`.

- [ ] **Paso 3: Implementación mínima**

Crear `web/src/chat/AvisoConversacionRetenida.tsx`:

```tsx
import { useState } from 'react';
import { Alert, Button, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { iniciarConversacion } from '../api/chat';
import { esRetenida } from './estadoConversacion';

type Resultado = 'inicial' | 'sigueRetenida' | 'error';

export function AvisoConversacionRetenida({
  avisoId,
  alLiberar,
}: {
  avisoId: string;
  alLiberar: () => void;
}) {
  const [resultado, setResultado] = useState<Resultado>('inicial');
  const [comprobando, setComprobando] = useState(false);

  async function comprobar() {
    setComprobando(true);
    setResultado('inicial');
    try {
      const conversacion = await iniciarConversacion(avisoId);
      if (esRetenida(conversacion.estado)) {
        setResultado('sigueRetenida');
        return;
      }
      alLiberar();
    } catch {
      setResultado('error');
    } finally {
      setComprobando(false);
    }
  }

  return (
    <Alert severity="info" sx={{ mb: 2 }}>
      <Typography>
        Tu mensaje está guardado, pero todavía no se ha entregado. Para que llegue, verifica tu
        identidad.
      </Typography>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1 }}>
        <Button component={RouterLink} to="/kyc" variant="contained" size="small">
          Verificar mi identidad
        </Button>
        <Button onClick={() => void comprobar()} disabled={comprobando} size="small">
          Ya verifiqué mi identidad
        </Button>
      </Stack>
      {resultado === 'sigueRetenida' && (
        <Typography variant="body2" sx={{ mt: 1 }}>
          Tu verificación todavía no está aprobada.
        </Typography>
      )}
      {resultado === 'error' && (
        <Typography variant="body2" color="error" sx={{ mt: 1 }}>
          No se pudo comprobar tu verificación. Inténtalo nuevamente.
        </Typography>
      )}
    </Alert>
  );
}
```

- [ ] **Paso 4: Ejecutar el test y verificar que pasa**

Desde `web/`:
- `npx vitest run src/chat/AvisoConversacionRetenida.test.tsx` → PASS, 4 tests.
- `npm run typecheck` → sin errores.

- [ ] **Paso 5: Commit**

Desde la raíz del repositorio:

```bash
./verify.ps1 -Changed
git add web/src/chat/AvisoConversacionRetenida.tsx web/src/chat/AvisoConversacionRetenida.test.tsx
git commit -m "feat(chat): añade el aviso de conversación retenida"
```

---

### Tarea 4: La conversación muestra el aviso y libera

**Archivos:**
- Modificar: `web/src/routes/ConversacionPage.tsx` (bloque de render alrededor de
  las líneas 261-285)
- Test: `web/src/routes/ConversacionPage.test.tsx`

**Interfaces:**
- Consume: `AvisoConversacionRetenida` (Tarea 3); `EstadoConversacion` y
  `esRetenida` (Tarea 1).
- Produce: nada para tareas posteriores.

- [ ] **Paso 1: Escribir el test que falla**

Añadir a `web/src/routes/ConversacionPage.test.tsx`, dentro del
`describe('ConversacionPage', ...)` existente:

```tsx
  it('avisa de la retención sin bloquear el compositor', async () => {
    vi.spyOn(chat, 'buscarConversacionPropia').mockResolvedValue({
      id: 'c1', avisoId: 'a1', contraparteId: 'otra-persona', rol: 'Comprador',
      creadaEn: '', ultimaActividadEn: '', ultimaSecuencia: 1, noLeidos: 0,
      estado: 3, origenCierre: null, puedeEnviar: true,
    });
    vi.spyOn(chat, 'obtenerMensajes').mockResolvedValue({ siguienteCursor: null, items: [] });

    montar();

    expect(await screen.findByText(/todavía no se ha entregado/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Verificar mi identidad' })).toBeInTheDocument();
    expect(screen.queryByText('Esta conversación no admite nuevos mensajes.')).toBeNull();
  });
```

`buscarConversacionPropia` devuelve `ConversacionResumen`, cuyos campos
`ultimaSecuenciaEntregadaContraparte` y `ultimaSecuenciaLeidaContraparte` son
opcionales; por eso el fixture no los declara, igual que el test preexistente
del archivo.

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `web/`: `npx vitest run src/routes/ConversacionPage.test.tsx`

Esperado: FAIL en el test nuevo — el texto del aviso no existe. El test
preexistente sigue en verde.

- [ ] **Paso 3: Implementación mínima**

En `web/src/routes/ConversacionPage.tsx`:

1. Añadir importaciones:

```tsx
import { AvisoConversacionRetenida } from '../chat/AvisoConversacionRetenida';
import { EstadoConversacion, esRetenida } from '../chat/estadoConversacion';
```

2. Insertar el aviso justo después del bloque `{!puedeEnviar && (...)}` y antes
   de `<InvitacionNotificaciones />`:

```tsx
      {esRetenida(conversacion.estado) && (
        <AvisoConversacionRetenida
          avisoId={conversacion.avisoId}
          alLiberar={() => {
            void queryClient.invalidateQueries({ queryKey: ['chat-conversacion', id] });
            void queryClient.invalidateQueries({ queryKey: ['chat-bandeja'] });
          }}
        />
      )}
```

3. Migrar los literales del bloque de acciones:

```tsx
        {conversacion.estado === EstadoConversacion.Cerrada ? (
          <Button onClick={() => accion.mutate('reabrir')}>Reabrir conversación</Button>
        ) : (
          <Button
            onClick={() => accion.mutate('cerrar')}
            disabled={conversacion.estado === EstadoConversacion.CerradaPorModeracion}
          >
            Cerrar conversación
          </Button>
        )}
```

No tocar nada más del archivo: ni el compositor, ni el efecto de tiempo real, ni
los indicadores de entrega.

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `web/`:
- `npx vitest run src/routes/ConversacionPage.test.tsx` → PASS, 2 tests.
- `npm run test` → suite completa en verde.
- `npm run typecheck` → sin errores.
- `npm run lint` → sin errores.
- `npm run build` → build correcto.

- [ ] **Paso 5: Commit**

Desde la raíz del repositorio:

```bash
./verify.ps1 -Changed
git add web/src/routes/ConversacionPage.tsx web/src/routes/ConversacionPage.test.tsx
git commit -m "feat(chat): avisa de la retención dentro de la conversación"
```

---

## Cierre

- [ ] Ejecutar la suite completa de frontend desde `web/`: `npm run test`.
- [ ] Revisar el diff completo contra el spec: alcance, copy literal, anti-PII,
      ausencia de cambios en backend o contrato.
- [ ] `git diff --check` y árbol de trabajo limpio.
- [ ] Informar: criterios de aceptación cubiertos, commits creados,
      verificaciones ejecutadas y lo que quede pendiente.

**No hacer push.** `develop` se publica solo cuando el usuario lo pida.

## Cobertura de los criterios de aceptación

| Criterio del spec | Dónde se cubre |
|---|---|
| 1. Aviso con CTA y compositor habilitado | Tarea 4, paso 1 |
| 2. Check «Enviado» en los mensajes | Sin cambios: `estadoMensaje.ts` intacto |
| 3. Chip «En espera de verificación» | Tarea 2, paso 1 |
| 4. El reintento libera y refresca | Tarea 3 (caso 2) y Tarea 4 (`alLiberar`) |
| 5. Sin verificar, informa sin error | Tarea 3, caso 3 |
| 6. Otros estados sin cambios | Tarea 1 (etiquetas), Tarea 2 y 4 (tests preexistentes) |
| 7. El test falla si se reordena el enum | Tarea 1, paso 1 |
