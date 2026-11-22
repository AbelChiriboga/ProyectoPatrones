import { useMemo, useState } from 'react';
import { createUseCases } from '../application/useCases.js';
import { createBrowserSession } from '../infrastructure/browserSession.js';
import { createHttpPosApi } from '../infrastructure/httpPosApi.js';
import LoginPage from './LoginPage.jsx';
import PosPage from './PosPage.jsx';
import AdminReportPage from './AdminReportPage.jsx';
import { createPendingSale } from '../infrastructure/pendingSale.js';
import { resolveApiBaseUrl } from '../infrastructure/apiUrl.js';

const API_URL = resolveApiBaseUrl();

// Aquí se "compone" la aplicación: se elige el adaptador HTTP y el de sesión y se inyectan en los casos de uso.
export default function App() {
  const session = useMemo(() => createBrowserSession(), []);
  const [username, setUsername] = useState(() => session.read()?.username ?? null);
  const [role, setRole] = useState(() => session.read()?.role ?? null);

  const useCases = useMemo(() => {
    const api = createHttpPosApi({
      baseUrl: API_URL,
      getToken: () => session.read()?.token ?? null,
      onUnauthorized: () => {
        session.clear();
        setUsername(null);
        setRole(null);
      },
    });
    return createUseCases({ api, session, pending: createPendingSale() });
  }, [session]);

  const handleLoggedIn = (uname) => {
    setUsername(uname);
    setRole(session.read()?.role ?? 'Cashier');
  };

  const handleLogout = () => {
    useCases.logout();
    setUsername(null);
    setRole(null);
  };

  if (!username) {
    return <LoginPage useCases={useCases} onLoggedIn={handleLoggedIn} />;
  }

  if (role === 'Admin') {
    return <AdminReportPage useCases={useCases} username={username} onLogout={handleLogout} />;
  }

  return <PosPage useCases={useCases} username={username} onLogout={handleLogout} />;
}
