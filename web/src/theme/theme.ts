import { createTheme } from '@mui/material/styles';

const colores = {
  pino: '#165C3B',
  pinoOscuro: '#0D422B',
  pinoClaro: '#DCEBE2',
  terracota: '#D75A2D',
  terracotaOscura: '#AC3F1B',
  crema: '#F8F6F1',
  papel: '#FFFEFC',
  grafito: '#1D2924',
  salvia: '#5E6B64',
  borde: '#DEDCD5',
  blanco: '#FFFFFF',
} as const;

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: colores.pino,
      dark: colores.pinoOscuro,
      light: colores.pinoClaro,
      contrastText: colores.blanco,
    },
    secondary: {
      main: colores.terracota,
      dark: colores.terracotaOscura,
      contrastText: colores.blanco,
    },
    background: {
      default: colores.crema,
      paper: colores.papel,
    },
    text: {
      primary: colores.grafito,
      secondary: colores.salvia,
    },
    divider: colores.borde,
  },
  typography: {
    fontFamily: '"Open Sans", Arial, sans-serif',
    h1: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 700,
      letterSpacing: '-0.03em',
    },
    h2: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 700,
      letterSpacing: '-0.025em',
    },
    h3: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 700,
      letterSpacing: '-0.02em',
    },
    h4: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 600,
      letterSpacing: '-0.02em',
    },
    h5: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 600,
      letterSpacing: '-0.015em',
    },
    h6: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 600,
      letterSpacing: '-0.01em',
    },
    button: {
      fontWeight: 700,
      textTransform: 'none',
    },
  },
  shape: {
    borderRadius: 12,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          minWidth: '320px',
          backgroundColor: colores.crema,
        },
        '::selection': {
          backgroundColor: colores.pinoClaro,
          color: colores.pinoOscuro,
        },
      },
    },
    MuiAppBar: {
      defaultProps: {
        color: 'default',
        elevation: 0,
      },
    },
    MuiButton: {
      defaultProps: {
        disableElevation: true,
      },
      styleOverrides: {
        root: {
          borderRadius: '12px',
          minHeight: '40px',
          transition: 'background-color 160ms ease, border-color 160ms ease, transform 160ms ease',
          '&:active': {
            transform: 'translateY(1px)',
          },
          '@media (prefers-reduced-motion: reduce)': {
            transition: 'none',
          },
        },
      },
    },
    MuiIconButton: {
      styleOverrides: {
        root: {
          transition: 'background-color 160ms ease, transform 160ms ease',
          '&:active': {
            transform: 'translateY(1px)',
          },
          '@media (prefers-reduced-motion: reduce)': {
            transition: 'none',
          },
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: '12px',
          backgroundColor: colores.papel,
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: '16px',
          backgroundImage: 'none',
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
        },
      },
    },
    MuiTooltip: {
      defaultProps: {
        arrow: true,
      },
    },
  },
});
