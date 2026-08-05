import { Camera } from '@capacitor/camera';

export type EstadoPermisoCamara = 'concedido' | 'denegado' | 'solicitarEnAjustes';

interface EstadoNativoCamara {
  camera: string;
}

interface ProveedorPermisosCamara {
  checkPermissions(): Promise<EstadoNativoCamara>;
  requestPermissions(opciones: { permissions: ['camera'] }): Promise<EstadoNativoCamara>;
}

export async function solicitarPermisoCamara(
  proveedor: ProveedorPermisosCamara = Camera,
): Promise<EstadoPermisoCamara> {
  const actual = await proveedor.checkPermissions();
  if (actual.camera === 'granted') return 'concedido';

  const solicitado = await proveedor.requestPermissions({ permissions: ['camera'] });
  if (solicitado.camera === 'granted') return 'concedido';
  if (solicitado.camera === 'denied') return 'denegado';
  return 'solicitarEnAjustes';
}
