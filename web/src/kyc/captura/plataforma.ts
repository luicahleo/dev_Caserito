interface EntornoMovil {
  userAgent: string;
  platform: string;
  maxTouchPoints: number;
  tieneCamaraWeb: boolean;
}

function entornoActual(): EntornoMovil {
  return {
    userAgent: navigator.userAgent,
    platform: navigator.platform,
    maxTouchPoints: navigator.maxTouchPoints,
    tieneCamaraWeb: typeof navigator.mediaDevices?.getUserMedia === 'function',
  };
}

export function esMovilConCamara(entorno: EntornoMovil = entornoActual()): boolean {
  const movilDeclarado = /Android|iPhone|iPad|iPod/i.test(entorno.userAgent);
  const ipadConAgenteEscritorio = entorno.platform === 'MacIntel' && entorno.maxTouchPoints > 1;

  return (movilDeclarado || ipadConAgenteEscritorio) && entorno.tieneCamaraWeb;
}
