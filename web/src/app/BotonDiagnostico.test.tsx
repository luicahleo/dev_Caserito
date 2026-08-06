import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { BotonDiagnostico } from './BotonDiagnostico';

afterEach(() => {
  sessionStorage.clear();
  vi.restoreAllMocks();
});

describe('botón de diagnóstico', () => {
  it('no se muestra sin modo diagnóstico', () => {
    render(<BotonDiagnostico />);

    expect(screen.queryByRole('button', { name: 'Descargar diagnóstico' })).toBeNull();
  });

  it('descarga el diagnóstico al pulsarlo en modo diagnóstico', () => {
    sessionStorage.setItem('caserito.debug', '1');
    const crearUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:mock');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);

    render(<BotonDiagnostico />);
    fireEvent.click(screen.getByRole('button', { name: 'Descargar diagnóstico' }));

    expect(crearUrl).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
  });
});
