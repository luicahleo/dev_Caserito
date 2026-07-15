// Holder en memoria del access token. NO se persiste (ni localStorage): al
// recargar, la sesión se restaura vía refresh (cookie httpOnly).
let accessToken: string | null = null;

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function clearAccessToken(): void {
  accessToken = null;
}
