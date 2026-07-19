# Frontend web

Aplican primero las reglas del `AGENTS.md` raíz.

- React y TypeScript estricto; no usar `any` para eludir el contrato.
- MUI: `Grid size={{}}`, sin `item`; `slotProps.htmlInput` en lugar de
  `inputProps`; `flexWrap` de `Stack` dentro de `sx`.
- Estado remoto con TanStack Query y navegación con React Router.
- Tipos API siempre desde `src/api/schema.d.ts`; regenerar desde OpenAPI y
  convertir `number | string` con `Number()`.
- Textos visibles y comentarios en español correcto; no exponer PII.

Comandos desde este directorio:

```powershell
npm run generate:api
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Ejecutar tests dirigidos durante el cambio; `build` es obligatorio al integrar
rutas, contrato OpenAPI o configuración de Vite/PWA.
