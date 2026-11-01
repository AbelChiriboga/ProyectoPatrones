import { useMemo, useState } from 'react';
import { createUseCases } from '../application/useCases.js';
import { createBrowserSession } from '../infrastructure/browserSession.js';
import { createHttpPosApi } from '../infrastructure/httpPosApi.js';
import LoginPage from './LoginPage.jsx';
import PosPage from './PosPage.jsx';

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5038';

// Aquí se "compone" la aplicación: se elige el adaptador HTTP y el de sesión y se inyectan en los casos de uso.
export default function App() {
  const session = useMemo(() => createBrowserSession(), []);
  const [username, setUsername] = useState(() => session.read()?.username ?? null);

  const useCases = useMemo(() => {
    const api = createHttpPosApi({
      baseUrl: API_URL,
      getToken: () => session.read()?.token ?? null,
      onUnauthorized: () => {
        session.clear();
        setUsername(null);
      },
    });
    return createUseCases({ api, session });
  }, [session]);

  const handleLogout = () => {
    useCases.logout();
    setUsername(null);
  };

  return username ? (
    <PosPage useCases={useCases} username={username} onLogout={handleLogout} />
  ) : (
    <LoginPage useCases={useCases} onLoggedIn={setUsername} />
  );
}
