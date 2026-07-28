import { AppBar, Box, Button, Container, Toolbar, Typography } from '@mui/material';
import { Link as RouterLink, Outlet, useNavigate } from 'react-router-dom';
import { useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { ContadorChat } from '../chat/ContadorChat';
import { NotificacionesBadge } from '../notificaciones/NotificacionesBadge';
import { NotificacionesDropdown } from '../notificaciones/NotificacionesDropdown';

export function AppLayout() {
  const { estaAutenticado, cerrarSesion, tienePermiso } = useAuth();
  const navigate = useNavigate();
  const [anchorNotificaciones, setAnchorNotificaciones] = useState<HTMLElement | null>(null);

  const salir = async () => {
    await cerrarSesion();
    navigate('/');
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <AppBar position="static">
        <Toolbar>
          <Typography
            variant="h6"
            component={RouterLink}
            to="/"
            sx={{ color: 'inherit', textDecoration: 'none', flexShrink: 0 }}
          >
            CaseritoApp
          </Typography>
          <Box sx={{ flexGrow: 1, display: 'flex', gap: 1, ml: 3 }}>
            <Button color="inherit" component={RouterLink} to="/">
              Explorar
            </Button>
            {estaAutenticado && (
              <>
                <Button color="inherit" component={RouterLink} to="/publicar">
                  Publicar
                </Button>
                <Button color="inherit" component={RouterLink} to="/mis-avisos">
                  Mis avisos
                </Button>
                <Button color="inherit" component={RouterLink} to="/acuerdos">
                  Mis acuerdos
                </Button>
                <Button color="inherit" component={RouterLink} to="/busquedas-guardadas">
                  Alertas
                </Button>
                <ContadorChat />
                <NotificacionesBadge
                  onClick={(evento) => setAnchorNotificaciones(evento.currentTarget)}
                />
                {tienePermiso('publicaciones.moderar') && (
                  <Button color="inherit" component={RouterLink} to="/admin/moderacion">Moderación</Button>
                )}
                {tienePermiso('chat.moderar') && (
                  <Button color="inherit" component={RouterLink} to="/admin/moderacion-chat">
                    Moderación de chat
                  </Button>
                )}
              </>
            )}
          </Box>
          {estaAutenticado ? (
            <>
              <Button color="inherit" component={RouterLink} to="/perfil">
                Perfil
              </Button>
              <Button color="inherit" onClick={salir}>
                Salir
              </Button>
            </>
          ) : (
            <Button color="inherit" component={RouterLink} to="/login">
              Entrar
            </Button>
          )}
        </Toolbar>
      </AppBar>
      <NotificacionesDropdown
        open={Boolean(anchorNotificaciones)}
        anchorEl={anchorNotificaciones}
        onClose={() => setAnchorNotificaciones(null)}
      />
      <Container component="main" maxWidth={false} disableGutters sx={{ flexGrow: 1 }}>
        <Outlet />
      </Container>
    </Box>
  );
}
