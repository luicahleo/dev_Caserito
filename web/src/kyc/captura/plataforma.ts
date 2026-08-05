interface EntornoPwa {
  displayStandalone: boolean;
  iosStandalone: boolean;
  userAgent: string;
  platform: string;
  maxTouchPoints: number;
  tieneCamaraWeb: boolean;
}

interface NavigatorIos extends Navigator {
  standalone?: boolean;
}

function entornoActual(): EntornoPwa {
  const navegador = navigator as NavigatorIos;
  return {
    displayStandalone: window.matchMedia('(display-mode: standalone)').matches,
    iosStandalone: navegador.standalone === true,
    userAgent: navegador.userAgent,
    platform: navegador.platform,
    maxTouchPoints: navegador.maxTouchPoints,
    tieneCamaraWeb: typeof navegador.mediaDevices?.getUserMedia === 'function',
  };
}

export function esPwaMovilInstalada(entorno: EntornoPwa = entornoActual()): boolean {
  const instalada = entorno.displayStandalone || entorno.iosStandalone;
  const movilDeclarado = /Android|iPhone|iPad|iPod/i.test(entorno.userAgent);
  const ipadConAgenteEscritorio = entorno.platform === 'MacIntel' && entorno.maxTouchPoints > 1;

  return instalada && (movilDeclarado || ipadConAgenteEscritorio) && entorno.tieneCamaraWeb;
}
