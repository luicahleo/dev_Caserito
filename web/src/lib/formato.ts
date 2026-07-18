// Formatea un monto en bolivianos (BOB). El contrato expone el monto como number | string
// (double de OpenAPI), por eso se coacciona a número antes de formatear.
const formateador = new Intl.NumberFormat('es-BO', {
  style: 'currency',
  currency: 'BOB',
});

export function formatearBob(monto: number | string): string {
  return formateador.format(Number(monto));
}
