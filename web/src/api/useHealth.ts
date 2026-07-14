import { useQuery } from '@tanstack/react-query';
import { getHealth } from './health';

export function useHealth() {
  return useQuery({ queryKey: ['health'], queryFn: getHealth });
}
