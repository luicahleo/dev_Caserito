/** Rutas de backend que nunca deben recibir el fallback HTML de la PWA. */
export const rutasExcluidasFallbackPwa = [/^\/api(?:\/|$)/, /^\/health(?:\/|$)/];
