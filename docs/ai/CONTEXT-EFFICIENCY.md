# Economía de contexto y cuota

## Principio

Recuperar contexto en capas: índice breve, resultados filtrados y detalles solo
para los elementos seleccionados. Más contexto no equivale automáticamente a
mejor resultado.

## Reglas operativas

- Una feature o bloque por sesión.
- Abrir una sesión nueva tras cerrar/mergear un bloque.
- Buscar antes de leer; leer fragmentos antes que archivos enteros.
- No cargar directorios completos ni documentación histórica sin filtro.
- Guardar decisiones estables en specs y estado temporal en un único handoff.
- Evitar repetir en prompts reglas ya cargadas por los archivos de instrucciones.
- Pedir salidas breves y resumir logs, stack traces y diffs.
- Ejecutar tests dirigidos en el ciclo TDD y suites completas al cierre.
- Usar el nivel de razonamiento y modelo más económico que mantenga la calidad.
- Delegar solo subtareas independientes; múltiples agentes suelen consumir más.
- Desactivar MCP, plugins o herramientas que no sean útiles para la sesión.

## Memoria persistente

Una memoria automática debe ser opcional. Antes de instalarla:

1. Auditar qué captura y cuándo.
2. Verificar almacenamiento local, retención y borrado.
3. Excluir secretos, PII, tokens, reportes y salidas sensibles.
4. Medir tokens usados para capturar/resumir frente a tokens ahorrados.
5. Probarla primero en un repositorio sin datos sensibles.

Para este proyecto se prefiere inicialmente memoria explícita y versionada:
specs + planes + Git + handoff reemplazable. Un sistema tipo `claude-mem` puede
evaluarse después, pero no es requisito del workflow.

## Métrica simple por bloque

Registrar al cierre, cuando la herramienta lo muestre:

- porcentaje de cuota al inicio y al final;
- sesiones utilizadas;
- compactaciones o reinicios;
- documentos precargados;
- incidencias causadas por contexto faltante o antiguo.

Comparar varios bloques antes de atribuir ahorro a una sola técnica.
