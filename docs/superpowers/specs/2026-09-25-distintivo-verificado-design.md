# Diseño — Distintivo de verificado en listado, detalle y chat

Fecha: 2026-09-25
Serie: «verificación al contactar», bloque 4 (último)
Depende de: `docs/superpowers/specs/2026-09-19-kyc-resolucion-automatica-design.md`,
`docs/superpowers/specs/2026-09-21-chat-verificacion-contacto-design.md`

## 1. Contexto y objetivo

Los tres bloques anteriores construyeron confianza pero no la mostraron: el KYC
se resuelve solo, la conversación de un comprador sin verificar se retiene y la
administración recibe aviso de lo que espera revisión. Nada de eso es visible
para quien explora el marketplace.

Este bloque cierra la serie llevando el sello de identidad verificada a donde se
toman las decisiones: al elegir qué aviso abrir, al mirar un aviso concreto y al
conversar con alguien.

### Serie completa

1. Resolución automática de KYC — cerrado.
2. Conversación retenida, backend y UI web — cerrado.
3. Aviso al administrador de solicitudes en espera — cerrado.
4. Distintivo de verificado — **este spec**.

## 2. No objetivos

- Marcar lo **no** verificado. La ausencia del sello no es una acusación.
- Distintivo en la bandeja de conversaciones: exigiría una consulta por fila
  contra un endpoint con rate limit.
- Cambiar quién puede publicar o contactar.
- Niveles o grados de verificación.

## 3. Decisiones

1. **La fuente es `EstaVerificadoAsync`, no `EstaHabilitadoParaMarketplaceAsync`.**
   El segundo devuelve `true` también para el rol `AdminPlataforma`
   (`ConsultaVerificacionKycEfCore.cs:23-35`), y un administrador no debe lucir
   un sello de identidad que no tiene. Es además el criterio que ya alimenta
   `verificado` en el perfil público, de modo que todas las superficies dicen lo
   mismo.
2. **El dato ya es público.** `GET /api/perfiles/{id}` expone `verificado` en
   `PerfilPublicoConReputacionDto`. Este bloque no publica información nueva:
   la lleva a donde se decide.
3. **El listado se compone en el Host, con consulta en lote.** Catalog no puede
   consultar Identity. El precedente es `PerfilesPublicosEndpoints`, que
   «compone datos públicos de Identity y Reputation sin acoplar sus contextos».
   Para evitar N+1, el puerto de Identity gana un método que resuelve la página
   entera en una consulta.
4. **Catalog aporta `VendedorId`; el booleano vive en un DTO del Host.** Poner
   `VendedorVerificado` en el DTO de Catalog obligaría a ese contexto a declarar
   un dato que no puede calcular, y a que alguien lo rellenara por fuera. En su
   lugar, `AvisoPublicoResumenDto` gana `VendedorId` —dato propio de Catalog, ya
   público en el detalle— y el Host devuelve un DTO de composición.
5. **Detalle y conversación consultan el perfil público desde el frontend.** Una
   llamada por pantalla, con un id concreto, a un endpoint que ya devuelve el
   dato. Evita ampliar el contrato en dos sitios más. En el listado no cabe esta
   solución: serían veinte llamadas por página.
6. **Un solo componente para el sello**, incluido el chip que hoy está inline en
   `PerfilPublicoPage.tsx:55`. Dos formas de pintar la misma señal divergen con
   el tiempo.

## 4. Comportamiento

| Superficie | Origen del dato | Presentación |
|---|---|---|
| Listado de avisos | `vendedorVerificado` del DTO compuesto por el Host | Icono con tooltip (modo compacto) |
| Detalle de aviso | `GET /api/perfiles/{vendedorId}` desde el frontend | Chip «Usuario verificado» |
| Conversación abierta | `GET /api/perfiles/{contraparteId}` desde el frontend | Chip junto a la contraparte |
| Perfil público | Ya existente | Mismo chip, ahora desde el componente |

Cuando el valor es `false`, **no se pinta nada**. Cuando la consulta del perfil
falla, tampoco: un error de red no debe convertirse en una señal negativa sobre
una persona.

### 4.1 El listado

`AvisoPublicoResumenDto` (`Catalog.Application/Avisos/DtosAvisoPublico.cs:6`)
gana `Guid VendedorId`, y la proyección de
`ConsultaAvisosPublicaEfCore.cs:63-75` lo incluye.

El Host define el DTO de composición, **plano**, y marca cada item:

```csharp
public sealed record AvisoPublicoResumenConVendedorDto(
    Guid Id,
    string Titulo,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion,
    IReadOnlyList<FotoAvisoDto> Fotos,
    Guid VendedorId,
    bool VendedorVerificado)
{
    public static AvisoPublicoResumenConVendedorDto Desde(
        AvisoPublicoResumenDto aviso, bool verificado) =>
        new(aviso.Id, aviso.Titulo, aviso.Monto, aviso.Moneda, aviso.NombreCategoria,
            aviso.NombreCiudad, aviso.Condicion, aviso.FechaCreacion, aviso.Fotos,
            aviso.VendedorId, verificado);
}
```

**Plano y no anidado** (`{ aviso, vendedorVerificado }`) a propósito: anidar
preserva igual de bien la separación de contextos, pero obliga al frontend a
desanidar en cada tarjeta y a rehacer todos los fixtures del listado. Con la
forma plana, para el consumidor el cambio es puramente aditivo: dos campos
nuevos y ninguno movido. El precio es repetir la lista de campos una vez en el
Host, dentro de un método de fábrica que el compilador verifica.

El puerto de Identity gana:

```csharp
Task<IReadOnlySet<Guid>> ObtenerVerificadosAsync(
    IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct);
```

Una consulta `WHERE UsuarioId IN (...)` por página, que devuelve solo los
verificados; con lista vacía devuelve un conjunto vacío sin ir a la base.

## 5. Componentes

| Archivo | Cambio |
|---|---|
| `Identity.Application/Kyc/IConsultaVerificacionKyc.cs` | Añade `ObtenerVerificadosAsync` |
| `Identity.Infrastructure/Kyc/ConsultaVerificacionKycEfCore.cs` | Implementa la consulta en lote |
| `Catalog.Application/Avisos/DtosAvisoPublico.cs` | `AvisoPublicoResumenDto` gana `VendedorId` |
| `Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs` | Proyecta `VendedorId` |
| `Host/Endpoints/AvisosEndpoints.cs` | DTO de composición y marcado de la página |
| `web/src/api/schema.d.ts` | Regenerado |
| `web/src/perfil/DistintivoVerificado.tsx` | Nuevo, con prop `compacto` |
| `web/src/routes/ExplorarPage.tsx` | Icono en la tarjeta |
| `web/src/routes/DetalleAvisoPage.tsx` | Consulta el perfil y pinta el chip |
| `web/src/routes/ConversacionPage.tsx` | Consulta el perfil de la contraparte |
| `web/src/routes/PerfilPublicoPage.tsx` | Migra el chip inline al componente |

## 6. Contrato

**Primer bloque de la serie que cambia el contrato OpenAPI.** El listado público
pasa a devolver el DTO de composición, y el resumen gana `vendedorId`. Hay que
regenerar `web/src/api/schema.d.ts` con `npm run generate:api`; el job `contract`
de CI valida que lo generado coincida con el artefacto del backend.

El cambio es aditivo para el consumidor: el item del listado conserva todos sus
campos en el mismo sitio y gana `vendedorId` y `vendedorVerificado`. Solo cambia
el nombre del esquema, que el cliente generado resuelve solo.

## 7. Seguridad y PII

- No se expone información nueva: `verificado` ya es público en el perfil y
  `vendedorId` ya viaja en el detalle del aviso.
- El distintivo es un booleano derivado. No revela el estado de la solicitud, su
  score, su motivo de revisión ni la fecha de aprobación.
- No se marca lo no verificado: no se publica ningún juicio negativo.
- La consulta en lote recibe únicamente ids de vendedores de la página que el
  solicitante ya obtuvo.

## 8. Pruebas

### Tests existentes afectados

`AvisoPublicoResumenDto` es un `record` posicional: **cualquier construcción a
mano se romperá al añadir `VendedorId`**. Antes de implementar hay que
localizarlas con una búsqueda dirigida sobre `AvisoPublicoResumenDto(` en
`CaseritoApp/tests` y adaptarlas.

En el frontend, los fixtures de `ExplorarPage.test.tsx` solo necesitan los dos
campos nuevos: al ser el DTO plano, ninguna propiedad existente se mueve.
`PerfilPublicoPage.test.tsx` sigue verde si el componente conserva el texto
exacto «Usuario verificado».

### Backend

- `ObtenerVerificadosAsync` con tres usuarios de los que dos tienen KYC aprobado
  devuelve exactamente esos dos.
- Con colección vacía devuelve conjunto vacío.
- Integración del listado público: un vendedor con KYC aprobado llega con
  `vendedorVerificado: true`; **un vendedor que solo es `AdminPlataforma` llega
  con `false`**. Este último es el test que distingue `EstaVerificadoAsync` de
  `EstaHabilitadoParaMarketplaceAsync` y el que fallaría si alguien cambia el
  método por el otro.

### Frontend

- `DistintivoVerificado`: chip con texto en modo normal; icono con nombre
  accesible en compacto; nada cuando `verificado` es `false`.
- Tarjeta del listado con y sin distintivo.
- Detalle de aviso que lo muestra tras resolverse el perfil.
- Conversación que lo muestra junto a la contraparte.
- Un fallo de la consulta de perfil no pinta distintivo ni rompe la pantalla.

## 9. Criterios de aceptación

1. Una tarjeta cuyo vendedor tiene KYC aprobado muestra el distintivo compacto
   con nombre accesible.
2. Una tarjeta cuyo vendedor no lo tiene no muestra nada.
3. El detalle de aviso muestra el chip «Usuario verificado» cuando corresponde.
4. La conversación abierta lo muestra junto a la contraparte.
5. El perfil público sigue mostrando el mismo sello, desde el componente
   compartido.
6. Un vendedor que solo es administrador de plataforma no aparece como
   verificado.
7. Un fallo al consultar el perfil no pinta distintivo ni rompe la pantalla.
8. El listado resuelve la verificación de toda la página en una sola consulta.

## 10. Riesgos y diferidos

- **El sello será casi ubicuo en avisos.** Publicar exige identidad habilitada
  (`AvisosEndpoints.cs:119`), así que casi todo vendedor lo tendrá. Funciona como
  sello de confianza, no como discriminador; donde sí discrimina es en el chat,
  porque la contraparte puede ser un comprador sin verificar. Asumido al fijar el
  alcance.
- **Una llamada extra por pantalla** en detalle y conversación, contra un
  endpoint con rate limit. Es una por pantalla y TanStack Query la cachea, pero
  no conviene extender el patrón a listados.
- **El criterio 8 no se asierta contando consultas.** La garantía es
  estructural: el método en lote recibe la página entera. Se cubre con un test
  unitario del método, no midiendo SQL en integración.
- **Bandeja de conversaciones sin distintivo.** Si se quisiera, pediría el mismo
  tratamiento en lote que el listado de avisos.
