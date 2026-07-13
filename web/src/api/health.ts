import { getJson } from './client';

export interface HealthStatus {
  estado: string;
}

export function getHealth(): Promise<HealthStatus> {
  return getJson<HealthStatus>('/health');
}
