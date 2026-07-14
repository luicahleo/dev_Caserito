// Cliente HTTP mínimo hacia la Web API. En Fase 1, cuando existan endpoints de
// negocio, se reemplaza/complementa con un cliente tipado generado del OpenAPI.
export async function getJson<T>(ruta: string): Promise<T> {
  const respuesta = await fetch(ruta, { headers: { Accept: 'application/json' } });
  if (!respuesta.ok) {
    throw new Error(`Petición fallida (${respuesta.status}) a ${ruta}`);
  }
  return (await respuesta.json()) as T;
}
