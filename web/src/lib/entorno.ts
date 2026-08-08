export function esEntornoDesarrollo(
  entorno: string | undefined = import.meta.env.VITE_CASERITO_ENVIRONMENT,
): boolean {
  return entorno?.trim().toLowerCase() === 'development';
}
