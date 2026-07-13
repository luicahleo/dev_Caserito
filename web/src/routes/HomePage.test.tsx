import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { HomePage } from './HomePage';

describe('HomePage', () => {
  it('muestra el título de CaseritoApp', () => {
    render(<HomePage />);
    expect(screen.getByText(/CaseritoApp/i)).toBeInTheDocument();
  });
});
