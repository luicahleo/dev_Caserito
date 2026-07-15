import { type ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from './AuthContext';

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { estaAutenticado, cargando } = useAuth();
  if (cargando) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }
  if (!estaAutenticado) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}
