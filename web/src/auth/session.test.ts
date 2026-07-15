import { describe, it, expect, afterEach } from 'vitest';
import { getAccessToken, setAccessToken, clearAccessToken } from './session';

afterEach(() => clearAccessToken());

describe('session', () => {
  it('guarda y devuelve el token en memoria', () => {
    expect(getAccessToken()).toBeNull();
    setAccessToken('abc');
    expect(getAccessToken()).toBe('abc');
  });
  it('clear lo borra', () => {
    setAccessToken('abc');
    clearAccessToken();
    expect(getAccessToken()).toBeNull();
  });
});
