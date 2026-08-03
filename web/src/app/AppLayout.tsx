import { Suspense, useState } from 'react';
import {
  AppBar,
  Box,
  Button,
  Container,
  Divider,
  Link,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Skeleton,
  Stack,
  Toolbar,
  Typography,
} from '@mui/material';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import AdminPanelSettingsOutlinedIcon from '@mui/icons-material/AdminPanelSettingsOutlined';
import BookmarkBorderRoundedIcon from '@mui/icons-material/BookmarkBorderRounded';
import ForumOutlinedIcon from '@mui/icons-material/ForumOutlined';
import HandshakeOutlinedIcon from '@mui/icons-material/HandshakeOutlined';
import Inventory2OutlinedIcon from '@mui/icons-material/Inventory2Outlined';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import PersonOutlineRoundedIcon from '@mui/icons-material/PersonOutlineRounded';
import StorefrontRoundedIcon from '@mui/icons-material/StorefrontRounded';
import { Link as RouterLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ContadorChat } from '../chat/ContadorChat';
import { NotificacionesBadge } from '../notificaciones/NotificacionesBadge';
import { NotificacionesDropdown } from '../notificaciones/NotificacionesDropdown';

export function CargandoRuta() {
  return (
    <Container
      component="section"
      maxWidth="lg"
      role="status"
      aria-label="Cargando página"
      sx={{ py: { xs: 3, md: 5 } }}
    >
      <Stack spacing={2}>
        <Skeleton variant="text" width="min(70%, 420px)" height={52} />
        <Skeleton variant="rounded" height={180} sx={{ borderRadius: '16px' }} />
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <Skeleton variant="rounded" height={220} sx={{ borderRadius: '16px', flex: 1 }} />
          <Skeleton variant="rounded" height={220} sx={{ borderRadius: '16px', flex: 1 }} />
        </Stack>
      </Stack>
    </Container>
  );
}

export function AppLayout() {
  const { estaAutenticado, cerrarSesion, tienePermiso } = useAuth();
  const navigate = useNavigate();
  const [anchorNotificaciones, setAnchorNotificaciones] = useState<HTMLElement | null>(null);
  const [anchorCuenta, setAnchorCuenta] = useState<HTMLElement | null>(null);

  const cerrarMenuCuenta = () => setAnchorCuenta(null);

  const salir = async () => {
    cerrarMenuCuenta();
    await cerrarSesion();
    navigate('/');
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100dvh' }}>
      <AppBar
        position="sticky"
        sx={{
          bgcolor: 'background.paper',
          borderBottom: 1,
          borderColor: 'divider',
          color: 'text.primary',
        }}
      >
        <Container maxWidth="xl">
          <Toolbar disableGutters sx={{ minHeight: { xs: 64, md: 72 }, gap: { xs: 1, sm: 1.5 } }}>
            <Stack
              component={RouterLink}
              to="/"
              direction="row"
              spacing={1}
              aria-label="CaseritoApp, inicio"
              sx={{
                alignItems: 'center',
                color: 'text.primary',
                flexShrink: 0,
                textDecoration: 'none',
              }}
            >
              <Box
                sx={{
                  alignItems: 'center',
                  bgcolor: 'primary.main',
                  borderRadius: '12px',
                  color: 'primary.contrastText',
                  display: 'flex',
                  height: 38,
                  justifyContent: 'center',
                  width: 38,
                }}
              >
                <StorefrontRoundedIcon sx={{ color: 'inherit', fontSize: 23 }} />
              </Box>
              <Typography
                variant="h6"
                component="span"
                sx={{ display: { xs: 'none', sm: 'block' }, lineHeight: 1 }}
              >
                CaseritoApp
              </Typography>
            </Stack>

            <Button
              color="inherit"
              component={RouterLink}
              to="/"
              sx={{ display: { xs: estaAutenticado ? 'none' : 'inline-flex', md: 'inline-flex' } }}
            >
              Explorar
            </Button>

            <Box sx={{ flexGrow: 1 }} />

            {estaAutenticado ? (
              <>
                <Button
                  aria-label="Publicar aviso"
                  color="secondary"
                  component={RouterLink}
                  startIcon={<AddRoundedIcon sx={{ color: 'inherit' }} />}
                  to="/publicar"
                  variant="contained"
                  sx={{
                    minWidth: { xs: 42, sm: 'auto' },
                    px: { xs: 1, sm: 2 },
                    '& .MuiButton-startIcon': { mr: { xs: 0, sm: 1 } },
                  }}
                >
                  <Box component="span" sx={{ display: { xs: 'none', sm: 'inline' } }}>
                    Publicar
                  </Box>
                </Button>

                <Box sx={{ display: { xs: 'none', lg: 'block' } }}>
                  <ContadorChat />
                </Box>

                <NotificacionesBadge
                  onClick={(evento) => setAnchorNotificaciones(evento.currentTarget)}
                />

                <Button
                  aria-controls={anchorCuenta ? 'menu-cuenta' : undefined}
                  aria-expanded={anchorCuenta ? 'true' : undefined}
                  aria-haspopup="menu"
                  color="inherit"
                  onClick={(evento) => setAnchorCuenta(evento.currentTarget)}
                  startIcon={<PersonOutlineRoundedIcon sx={{ color: 'inherit' }} />}
                  sx={{
                    minWidth: { xs: 42, sm: 'auto' },
                    px: { xs: 1, sm: 1.5 },
                    '& .MuiButton-startIcon': { mr: { xs: 0, sm: 1 } },
                  }}
                >
                  <Box component="span" sx={{ display: { xs: 'none', sm: 'inline' } }}>
                    Mi cuenta
                  </Box>
                </Button>
              </>
            ) : (
              <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                <Button component={RouterLink} to="/login" variant="outlined">
                  Entrar
                </Button>
              </Stack>
            )}
          </Toolbar>
        </Container>
      </AppBar>

      {estaAutenticado && (
        <>
          <NotificacionesDropdown
            open={Boolean(anchorNotificaciones)}
            anchorEl={anchorNotificaciones}
            onClose={() => setAnchorNotificaciones(null)}
          />
          <Menu
            id="menu-cuenta"
            anchorEl={anchorCuenta}
            open={Boolean(anchorCuenta)}
            onClose={cerrarMenuCuenta}
            slotProps={{
              paper: {
                sx: {
                  border: 1,
                  borderColor: 'divider',
                  mt: 1,
                  minWidth: 240,
                },
              },
            }}
          >
            <MenuItem component={RouterLink} to="/perfil" onClick={cerrarMenuCuenta}>
              <ListItemIcon>
                <PersonOutlineRoundedIcon sx={{ color: 'text.secondary' }} />
              </ListItemIcon>
              <ListItemText>Mi perfil</ListItemText>
            </MenuItem>
            <MenuItem component={RouterLink} to="/mis-avisos" onClick={cerrarMenuCuenta}>
              <ListItemIcon>
                <Inventory2OutlinedIcon sx={{ color: 'text.secondary' }} />
              </ListItemIcon>
              <ListItemText>Mis avisos</ListItemText>
            </MenuItem>
            <MenuItem component={RouterLink} to="/acuerdos" onClick={cerrarMenuCuenta}>
              <ListItemIcon>
                <HandshakeOutlinedIcon sx={{ color: 'text.secondary' }} />
              </ListItemIcon>
              <ListItemText>Mis acuerdos</ListItemText>
            </MenuItem>
            <MenuItem component={RouterLink} to="/busquedas-guardadas" onClick={cerrarMenuCuenta}>
              <ListItemIcon>
                <BookmarkBorderRoundedIcon sx={{ color: 'text.secondary' }} />
              </ListItemIcon>
              <ListItemText>Alertas guardadas</ListItemText>
            </MenuItem>
            <MenuItem
              component={RouterLink}
              to="/mensajes"
              onClick={cerrarMenuCuenta}
              sx={{ display: { lg: 'none' } }}
            >
              <ListItemIcon>
                <ForumOutlinedIcon sx={{ color: 'text.secondary' }} />
              </ListItemIcon>
              <ListItemText>Mensajes</ListItemText>
            </MenuItem>

            {(tienePermiso('publicaciones.moderar') || tienePermiso('chat.moderar')) && <Divider />}

            {tienePermiso('publicaciones.moderar') && (
              <MenuItem component={RouterLink} to="/admin/moderacion" onClick={cerrarMenuCuenta}>
                <ListItemIcon>
                  <AdminPanelSettingsOutlinedIcon sx={{ color: 'text.secondary' }} />
                </ListItemIcon>
                <ListItemText>Moderación</ListItemText>
              </MenuItem>
            )}
            {tienePermiso('chat.moderar') && (
              <MenuItem
                component={RouterLink}
                to="/admin/moderacion-chat"
                onClick={cerrarMenuCuenta}
              >
                <ListItemIcon>
                  <ForumOutlinedIcon sx={{ color: 'text.secondary' }} />
                </ListItemIcon>
                <ListItemText>Moderación de chat</ListItemText>
              </MenuItem>
            )}

            <Divider />
            <MenuItem onClick={salir}>
              <ListItemIcon>
                <LogoutRoundedIcon sx={{ color: 'text.secondary' }} />
              </ListItemIcon>
              <ListItemText>Salir</ListItemText>
            </MenuItem>
          </Menu>
        </>
      )}

      <Container
        component="main"
        maxWidth={false}
        disableGutters
        sx={{ flexGrow: 1, width: '100%' }}
      >
        <Suspense fallback={<CargandoRuta />}>
          <Outlet />
        </Suspense>
      </Container>

      <Box
        component="footer"
        sx={{
          bgcolor: 'primary.dark',
          color: 'primary.contrastText',
          mt: 'auto',
          py: { xs: 4, md: 5 },
        }}
      >
        <Container maxWidth="lg">
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={{ xs: 3, md: 6 }}
            sx={{
              alignItems: { xs: 'flex-start', md: 'center' },
              justifyContent: 'space-between',
            }}
          >
            <Stack spacing={0.75} sx={{ maxWidth: 420 }}>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <StorefrontRoundedIcon sx={{ color: 'secondary.main', fontSize: 28 }} />
                <Typography variant="h6">CaseritoApp</Typography>
              </Stack>
              <Typography variant="body2" sx={{ color: 'primary.light' }}>
                Compra y vende cerca de ti, con información clara y acuerdos entre personas.
              </Typography>
            </Stack>

            <Stack
              component="nav"
              aria-label="Enlaces legales"
              direction="row"
              spacing={{ xs: 1.5, sm: 2.5 }}
              useFlexGap
              sx={{ flexWrap: 'wrap' }}
            >
              {[
                ['Privacidad', '/privacidad'],
                ['Términos', '/terminos'],
                ['Cookies', '/cookies'],
                ['Contacto', '/contacto'],
                ['Eliminar mis datos', '/eliminacion-de-datos'],
              ].map(([etiqueta, ruta]) => (
                <Link
                  key={ruta}
                  component={RouterLink}
                  to={ruta}
                  underline="hover"
                  sx={{ color: 'primary.contrastText', fontSize: '0.875rem' }}
                >
                  {etiqueta}
                </Link>
              ))}
            </Stack>
          </Stack>
        </Container>
      </Box>
    </Box>
  );
}
