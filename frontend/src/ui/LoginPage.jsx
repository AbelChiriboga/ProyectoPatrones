import { useState } from 'react';

export default function LoginPage({ useCases, onLoggedIn }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');
    setLoading(true);
    try {
      onLoggedIn(await useCases.login(username, password));
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="login">
      <form className="card login__form" onSubmit={handleSubmit}>
        <h1>POS Odoo</h1>
        <p className="muted">Inicie sesión como operador</p>

        <label>
          Usuario
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" autoFocus />
        </label>
        <label>
          Contraseña
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
        </label>

        {error && <p className="error" role="alert">{error}</p>}

        <button type="submit" disabled={loading}>{loading ? 'Ingresando…' : 'Ingresar'}</button>
      </form>
    </main>
  );
}
