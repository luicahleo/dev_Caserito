import { Alert, Container, Link, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import type { ReactNode } from 'react';

const fechaActualizacion = '3 de agosto de 2026';

function PaginaLegal({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <Container maxWidth="md" sx={{ py: 5 }}>
      <Stack spacing={3}>
        <div>
          <Typography variant="h3" component="h1" gutterBottom>
            {titulo}
          </Typography>
          <Typography color="text.secondary">
            Versión provisional · Última actualización: {fechaActualizacion}
          </Typography>
        </div>
        <Alert severity="info">
          Este texto es provisional y deberá revisarse con asesoría legal antes del lanzamiento
          público de CaseritoApp.
        </Alert>
        {children}
      </Stack>
    </Container>
  );
}

function Seccion({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section>
      <Typography variant="h5" component="h2" gutterBottom>
        {titulo}
      </Typography>
      <Typography component="div" color="text.secondary" sx={{ '& p': { mt: 0, mb: 1.5 } }}>
        {children}
      </Typography>
    </section>
  );
}

export function PrivacidadPage() {
  return (
    <PaginaLegal titulo="Política de privacidad">
      <Seccion titulo="Responsable y alcance">
        <p>
          CaseritoApp es una plataforma para publicar, descubrir y contactar sobre productos y
          acuerdos entre usuarios. Los datos identificativos y de contacto del responsable legal se
          incorporarán aquí antes del lanzamiento público.
        </p>
      </Seccion>
      <Seccion titulo="Datos que tratamos">
        <p>
          Podemos tratar nombre, correo electrónico, ciudad, datos de perfil, publicaciones,
          imágenes, búsquedas guardadas, mensajes, acuerdos, notificaciones, reportes y datos
          técnicos necesarios para proteger la sesión y operar el servicio.
        </p>
        <p>
          Cuando el usuario inicia sesión con Google o Facebook, recibimos únicamente los datos
          básicos autorizados, como un identificador del proveedor, nombre y correo electrónico.
          CaseritoApp no importa contactos, publicaciones ni actividad social, y no conserva los
          tokens de acceso de esos proveedores.
        </p>
        <p>
          Si se utiliza la verificación de identidad, pueden tratarse documentos e imágenes de
          verificación. Estos datos requieren medidas reforzadas y acceso restringido.
        </p>
      </Seccion>
      <Seccion titulo="Finalidades">
        <p>
          Usamos los datos para crear y proteger cuentas, permitir publicaciones y comunicaciones,
          gestionar acuerdos, prevenir fraude y abuso, moderar contenido, atender solicitudes y
          cumplir obligaciones legales aplicables.
        </p>
      </Seccion>
      <Seccion titulo="Conservación y terceros">
        <p>
          Conservamos los datos mientras la cuenta esté activa y durante los plazos necesarios para
          seguridad, resolución de controversias y obligaciones legales. Los plazos definitivos y
          los proveedores de infraestructura deberán detallarse antes del lanzamiento.
        </p>
        <p>
          Solo compartimos información con proveedores necesarios para prestar el servicio, con
          autoridades cuando exista obligación legal o con autorización del usuario. No vendemos
          datos personales.
        </p>
      </Seccion>
      <Seccion titulo="Derechos y contacto">
        <p>
          Puedes solicitar acceso, corrección o eliminación de tus datos mediante la página de{' '}
          <Link component={RouterLink} to="/eliminacion-de-datos">
            eliminación de datos
          </Link>{' '}
          o consultar los canales disponibles en{' '}
          <Link component={RouterLink} to="/contacto">
            Contacto
          </Link>
          . Verificaremos la identidad antes de atender solicitudes que afecten datos personales.
        </p>
      </Seccion>
    </PaginaLegal>
  );
}

export function TerminosPage() {
  return (
    <PaginaLegal titulo="Términos y condiciones">
      <Seccion titulo="Uso del servicio">
        <p>
          Al crear una cuenta o utilizar CaseritoApp, aceptas usar la plataforma de forma lícita,
          proporcionar información razonablemente exacta y proteger tus credenciales. Debes tener
          capacidad legal suficiente para realizar las operaciones que acuerdes.
        </p>
      </Seccion>
      <Seccion titulo="Publicaciones y conducta">
        <p>
          No está permitido publicar productos ilegales, contenido engañoso, discriminatorio o que
          infrinja derechos de terceros, ni usar la plataforma para fraude, acoso, spam o
          suplantación. Cada usuario es responsable del contenido que publica y de contar con
          autorización para utilizar sus textos e imágenes.
        </p>
      </Seccion>
      <Seccion titulo="Acuerdos entre usuarios">
        <p>
          CaseritoApp facilita el contacto y el registro de acuerdos, pero no es parte de la
          compraventa ni garantiza la identidad, calidad, legalidad, entrega o pago de los
          productos. Los usuarios deben revisar la información, acordar condiciones claras y tomar
          precauciones antes de completar una operación.
        </p>
      </Seccion>
      <Seccion titulo="Moderación y disponibilidad">
        <p>
          Podemos limitar, ocultar o retirar contenido y suspender cuentas cuando detectemos
          riesgos, incumplimientos o requerimientos legales. El servicio puede cambiar o
          interrumpirse por mantenimiento, seguridad o causas fuera de nuestro control.
        </p>
      </Seccion>
      <Seccion titulo="Cambios y consultas">
        <p>
          Podremos actualizar estos términos y publicaremos la fecha de la nueva versión. Las dudas
          o reclamaciones pueden presentarse mediante la página de{' '}
          <Link component={RouterLink} to="/contacto">
            Contacto
          </Link>
          .
        </p>
      </Seccion>
    </PaginaLegal>
  );
}

export function CookiesPage() {
  return (
    <PaginaLegal titulo="Política de cookies">
      <Seccion titulo="Cookies necesarias">
        <p>
          CaseritoApp utiliza cookies y mecanismos equivalentes estrictamente necesarios para
          autenticar al usuario, proteger el inicio de sesión, mantener la seguridad y recordar
          operaciones esenciales. Desactivarlos puede impedir el acceso o el funcionamiento correcto
          de la plataforma.
        </p>
      </Seccion>
      <Seccion titulo="Inicio de sesión externo">
        <p>
          Al elegir Google o Facebook, esos proveedores pueden utilizar sus propias cookies conforme
          a sus políticas. CaseritoApp emplea una cookie temporal y segura para completar el proceso
          de autenticación y la elimina al finalizarlo o cuando caduca.
        </p>
      </Seccion>
      <Seccion titulo="Analítica y publicidad">
        <p>
          Actualmente no utilizamos cookies propias de publicidad ni analítica de terceros. Si se
          incorporan en el futuro, esta política se actualizará y se solicitará consentimiento
          cuando corresponda.
        </p>
      </Seccion>
    </PaginaLegal>
  );
}

export function ContactoPage() {
  return (
    <PaginaLegal titulo="Contacto y soporte">
      <Seccion titulo="Cómo contactarnos">
        <p>
          El correo de soporte y los datos del responsable legal están pendientes de confirmación y
          se publicarán aquí antes de abrir CaseritoApp al público.
        </p>
        <p>
          Para solicitudes de privacidad o eliminación de datos deberás indicar el correo asociado a
          tu cuenta y describir la solicitud. No envíes contraseñas, tokens, documentos de
          identidad, datos de pago ni imágenes sensibles por correo.
        </p>
      </Seccion>
      <Alert severity="warning">
        Pendiente antes de producción: sustituir este aviso por un correo de soporte válido, la
        identidad del responsable y, cuando corresponda, su domicilio legal.
      </Alert>
    </PaginaLegal>
  );
}

export function EliminacionDatosPage() {
  return (
    <PaginaLegal titulo="Eliminación de cuenta y datos">
      <Seccion titulo="Solicitar la eliminación">
        <p>
          Puedes solicitar la eliminación de tu cuenta de CaseritoApp y de los datos asociados a
          ella mediante el canal que se publicará en la página de{' '}
          <Link component={RouterLink} to="/contacto">
            Contacto
          </Link>
          . La solicitud debe enviarse desde el correo asociado a la cuenta. Podremos pedir una
          comprobación adicional para evitar eliminaciones no autorizadas.
        </p>
      </Seccion>
      <Seccion titulo="Usuarios de Google y Facebook">
        <p>
          Desvincular CaseritoApp desde Google o Facebook impide futuros accesos con ese proveedor,
          pero no elimina automáticamente la cuenta ni los datos almacenados en CaseritoApp. Para
          eliminarlos debes seguir el procedimiento anterior.
        </p>
      </Seccion>
      <Seccion titulo="Qué ocurre después">
        <p>
          Eliminaremos o anonimizaremos los datos que ya no sean necesarios. Determinada información
          puede conservarse de forma limitada cuando sea necesaria para prevenir fraude, resolver
          controversias, proteger a otros usuarios, mantener copias de seguridad temporales o
          cumplir una obligación legal. Informaremos el resultado de la solicitud por un canal
          seguro.
        </p>
      </Seccion>
      <Alert severity="warning">
        Procedimiento provisional: antes de producción debe añadirse un canal operativo válido y
        definirse el plazo de respuesta y eliminación.
      </Alert>
    </PaginaLegal>
  );
}
