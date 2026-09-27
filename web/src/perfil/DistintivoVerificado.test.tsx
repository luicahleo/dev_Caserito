import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DistintivoVerificado } from './DistintivoVerificado';

describe('DistintivoVerificado', () => {
  it('muestra el sello con texto cuando el usuario está verificado', () => {
    render(<DistintivoVerificado verificado />);

    expect(screen.getByText('Usuario verificado')).toBeInTheDocument();
  });

  it('en modo compacto muestra solo el icono, con nombre accesible', () => {
    render(<DistintivoVerificado verificado compacto />);

    expect(screen.queryByText('Usuario verificado')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Usuario verificado')).toBeInTheDocument();
  });

  it('no muestra nada cuando el usuario no está verificado', () => {
    const { container } = render(<DistintivoVerificado verificado={false} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('tampoco muestra nada en modo compacto si no está verificado', () => {
    const { container } = render(<DistintivoVerificado verificado={false} compacto />);

    expect(container).toBeEmptyDOMElement();
  });
});
