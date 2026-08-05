import { Capacitor } from '@capacitor/core';

interface DetectorPlataforma {
  isNativePlatform(): boolean;
  getPlatform(): string;
}

export function esAndroidNativo(detector: DetectorPlataforma = Capacitor): boolean {
  return detector.isNativePlatform() && detector.getPlatform() === 'android';
}
