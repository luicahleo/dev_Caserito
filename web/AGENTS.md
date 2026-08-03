# Frontend web

Aplican primero las reglas del `AGENTS.md` raíz. Este archivo define el estándar
local para una experiencia homogénea, moderna y confiable de marketplace.

## Stack obligatorio

- React 19 y TypeScript estricto; no usar `any`, aserciones inseguras ni tipos
  duplicados para eludir contratos.
- Vite, React Router 7 declarativo, MUI 9 con Emotion, TanStack Query 5,
  React Hook Form + Zod y Vitest + Testing Library.
- No incorporar otra librería de UI, estado remoto, formularios, iconos o CSS
  sin autorización explícita.
- Antes de usar una API de una dependencia, comprobar la versión instalada en
  `package.json`; no asumir sintaxis de otras versiones.

## Organización y responsabilidades

- `src/routes/`: composición de páginas, parámetros de ruta y coordinación del
  flujo; evitar concentrar aquí componentes reutilizables o clientes HTTP.
- `src/api/`: acceso HTTP, adaptación mínima del contrato y errores genéricos;
  ningún componente debe llamar `fetch` directamente.
- Agrupar componentes, hooks y utilidades por dominio cuando tengan más de un
  consumidor (`avisos`, `chat`, `notificaciones`, etc.).
- `src/lib/`: utilidades puras y transversales. `src/theme/`: tokens globales.
- Extraer componentes cuando representen un patrón repetido o una unidad con
  comportamiento propio; no fragmentar JSX pequeño sin beneficio.
- Mantener lógica de negocio fuera de componentes visuales. Preferir funciones
  puras, hooks específicos y retornos tempranos frente a componentes extensos.

## Diseño visual del marketplace

- Priorizar una interfaz mobile-first, clara, rápida y basada en confianza:
  fotos y título del aviso, precio, ubicación, estado y acción principal deben
  tener jerarquía evidente.
- Reutilizar primero componentes MUI. Usar `Stack` para flex y espaciado;
  reservar `Box` para contenedores con semántica o estilo visual. Evitar `Grid`
  salvo que una cuadrícula bidimensional lo justifique.
- En MUI 9: `Grid size={{}}`, nunca `item`; usar `slotProps` en lugar de props
  obsoletas y colocar propiedades de sistema dentro de `sx`.
- Consultar `src/theme/theme.ts` antes de estilizar. Colores, tipografía,
  sombras, radios y superficies repetidos deben convertirse en tokens del tema;
  no dispersar valores hexadecimales ni estilos equivalentes por las páginas.
- Usar variantes tipográficas del tema y componentes semánticos (`Card`,
  `Button`, `Alert`, `Dialog`, `Skeleton`, etc.) antes de recrearlos.
- Para variantes visuales reutilizables, usar `styled` importado exclusivamente
  desde `@mui/material/styles`; el layout circunstancial permanece en `sx`.
- Radios explícitos siempre en píxeles. Evitar estilos inline con `style`, CSS
  global ad hoc y selectores dependientes de la estructura interna de MUI.
- Iconos desde `@mui/icons-material`; todo `IconButton` requiere nombre
  accesible. No dibujar SVG manualmente.
- Toda vista debe ser usable desde 320 px y adaptarse sin desbordamiento a
  móvil, tableta y escritorio. Evitar anchos y alturas rígidos sin motivo.
- Imágenes de avisos: reservar proporción para evitar saltos, usar `object-fit`,
  texto alternativo útil, carga diferida fuera del primer viewport y estados de
  error sin revelar rutas ni datos internos.
- No añadir animación decorativa que afecte rendimiento; respetar
  `prefers-reduced-motion` y limitar transiciones a feedback de interacción.

## Estados y experiencia de usuario

- Cada consulta o acción asíncrona debe contemplar carga, vacío, error, éxito y
  reintento cuando corresponda. Para listas y fichas usar `Skeleton` estable;
  no dejar pantallas en blanco ni saltos de layout.
- Una pantalla vacía debe explicar qué ocurrió y ofrecer una acción relevante.
  Los errores visibles deben ser breves, accionables y genéricos.
- Deshabilitar acciones mientras se envían, mostrar progreso y prevenir dobles
  envíos. Confirmar operaciones irreversibles o de impacto comercial.
- Conservar filtros, búsqueda y paginación en la URL cuando deban sobrevivir a
  recarga, historial o enlace compartido.
- Mantener textos y formatos en español correcto. Centralizar formatos de
  precio, fecha, distancia y ubicación en `src/lib/`; no concatenarlos a mano.
- No ocultar controles críticos solo en hover. Foco, selección, deshabilitado y
  error deben distinguirse visualmente y no depender únicamente del color.

## Datos, formularios y navegación

- Tipos API siempre desde `src/api/schema.d.ts`; regenerar desde OpenAPI. No
  editar el archivo generado manualmente. Convertir `number | string` con
  `Number()` únicamente en el límite donde sea necesario.
- Estado remoto con TanStack Query; incluir en `queryKey` todo parámetro que
  altere el resultado. Invalidar por prefijos coherentes tras mutaciones y no
  copiar respuestas remotas a estado local sin necesidad.
- Estado local solo para interacción efímera. No crear stores globales para
  datos que pertenecen a la URL, al servidor o a React Query.
- Formularios con React Hook Form y Zod. El esquema es la fuente de validación
  del cliente; asociar errores al campo y conservar un error general seguro.
- No confiar en validaciones del cliente para autorización, precios, identidad
  ni transiciones del negocio: el backend sigue siendo la autoridad.
- Declarar rutas en `src/app/router.tsx`, reutilizar `ProtectedRoute` y
  `RequierePermiso`, y navegar mediante React Router en vez de manipular
  `window.location`, salvo flujos externos que lo exijan.

## Accesibilidad y seguridad

- Usar HTML semántico y componentes MUI según su propósito. Toda interacción
  debe funcionar con teclado y tener nombre accesible; formularios con labels,
  diálogos titulados e imágenes con `alt` adecuado.
- Mantener foco visible, orden lógico y contraste suficiente. Probar controles
  por rol y nombre accesible, no por clases ni detalles de implementación.
- Anti-PII: nunca registrar ni mostrar en errores documentos, imágenes, tokens,
  pagos, credenciales, texto de reportes, identificadores técnicos ni contenido
  privado. No añadir `console.log` con respuestas o formularios.
- Renderizar solo acciones autorizadas y mantener las protecciones de ruta;
  ocultar UI no sustituye la autorización del backend.

## Pruebas y calidad

- Cambios de comportamiento siguen TDD: prueba dirigida roja, implementación
  mínima y prueba verde. Probar la conducta observable con Testing Library.
- Cubrir al menos estados principal, carga/error/vacío relevantes, permisos,
  navegación y regresiones del cambio. Evitar snapshots grandes y selectores
  frágiles.
- Mantener componentes pequeños, nombres claros en español coherente con el
  dominio y dependencias explícitas. No mezclar refactors ajenos al pedido.
- Antes de cerrar, revisar ausencia de mojibake, PII, APIs MUI obsoletas, estilos
  duplicados, desbordamientos móviles y estados asíncronos incompletos.

## Comandos

Ejecutar desde `web/`:

```powershell
npm run generate:api
npm run typecheck
npm run lint
npm run test -- --run
npm run format:check
npm run build
```

Durante el cambio, ejecutar primero la prueba dirigida. `generate:api` aplica
solo si cambió OpenAPI. `build` es obligatorio al integrar rutas, contrato,
tema global o configuración de Vite/PWA. Informar cualquier comando omitido y
su motivo; nunca afirmar que una verificación pasó sin haberla ejecutado.
