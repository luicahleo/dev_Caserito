const PREFIJO_INTENTO = 'caserito:recuperacion-carga:';
const VIGENCIA_INTENTO_MS = 5 * 60 * 1_000;

type AlmacenamientoRecuperacion = Pick<Storage, 'getItem' | 'setItem'>;

export function esErrorCargaDiferida(error: unknown): boolean {
  if (!(error instanceof Error)) return false;

  return /failed to fetch dynamically imported module|importing a module script failed|chunkloaderror|loading chunk [\d]+ failed/i.test(
    error.message,
  );
}

export function intentarRecuperarCarga(
  error: unknown,
  ruta: string,
  almacenamiento: AlmacenamientoRecuperacion = window.sessionStorage,
  recargar: () => void = () => window.location.reload(),
  ahora: () => number = Date.now,
): boolean {
  if (!esErrorCargaDiferida(error)) return false;

  const clave = `${PREFIJO_INTENTO}${ruta}`;

  try {
    const valorAnterior = almacenamiento.getItem(clave);
    const intentoAnterior = valorAnterior === null ? null : Number(valorAnterior);
    const instanteActual = ahora();
    if (
      intentoAnterior !== null &&
      Number.isFinite(intentoAnterior) &&
      instanteActual - intentoAnterior < VIGENCIA_INTENTO_MS
    ) {
      return false;
    }

    almacenamiento.setItem(clave, String(instanteActual));
    recargar();
    return true;
  } catch {
    return false;
  }
}
