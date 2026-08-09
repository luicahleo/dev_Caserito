import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EstadoEntregaMensaje } from './EstadoEntregaMensaje';

describe('EstadoEntregaMensaje', () => {
  it.each(['enviado', 'entregado', 'leido'] as const)(
    'expone el estado %s de forma accesible',
    (estado) => {
      render(<EstadoEntregaMensaje estado={estado} />);
      const etiqueta =
        estado === 'leido' ? 'Leído' : `${estado[0].toUpperCase()}${estado.slice(1)}`;
      expect(screen.getByLabelText(etiqueta)).toBeInTheDocument();
    },
  );
});
