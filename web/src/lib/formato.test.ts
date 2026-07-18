import { describe, it, expect } from 'vitest';
import { formatearBob } from './formato';

describe('formatearBob', () => {
  it('formatea un número como moneda boliviana', () => {
    expect(formatearBob(1234.5)).toContain('1.234,5');
  });

  it('acepta el monto como string (viene del contrato como number | string)', () => {
    expect(formatearBob('300')).toContain('300');
  });
});
