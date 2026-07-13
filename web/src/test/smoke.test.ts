import { describe, it, expect } from 'vitest';

// Test trivial para validar que el runner de Vitest funciona correctamente.
describe('runner', () => {
  it('funciona', () => {
    expect(1 + 1).toBe(2);
  });
});
