import { useState } from 'react';
import loginBg from './login-bg.jpg';

export default function LoginPage({ useCases, onLoggedIn }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
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
    <main className="login-screen">
      <div className="login-visual">
        <img src={loginBg} alt="Cafetería y Punto de Venta" className="login-visual__image" />
        <div className="login-visual__overlay">
          <p className="login-visual__tag">SISTEMA DE GESTIÓN INTERNO</p>
        </div>
      </div>

      <div className="login-panel">
        <form className="login-form" onSubmit={handleSubmit}>
          <div className="login-header">
            <h1 className="login-title">POS Odoo</h1>
            <p className="login-subtitle">TIENDA LOGIN</p>
          </div>

          <div className="login-input-group">
            <div className="login-field">
              <span className="login-field__icon" aria-hidden="true">
                <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                  <circle cx="12" cy="7" r="4" />
                </svg>
              </span>
              <input
                id="login-username"
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="Usuario o Correo Electrónico"
                autoComplete="username"
                autoFocus
                required
              />
            </div>

            <div className="login-field">
              <span className="login-field__icon" aria-hidden="true">
                <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
              </span>
              <input
                id="login-password"
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Contraseña"
                autoComplete="current-password"
                required
              />
              <button
                type="button"
                className="login-field__toggle"
                onClick={() => setShowPassword((prev) => !prev)}
                title={showPassword ? 'Ocultar contraseña' : 'Ver contraseña'}
                aria-label={showPassword ? 'Ocultar contraseña' : 'Ver contraseña'}
              >
                {showPassword ? (
                  <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                    <line x1="1" y1="1" x2="23" y2="23" />
                  </svg>
                ) : (
                  <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                    <circle cx="12" cy="12" r="3" />
                  </svg>
                )}
              </button>
            </div>
          </div>

          {error && <p className="notice notice-error" role="alert">{error}</p>}

          <button type="submit" className="login-submit-btn" disabled={loading}>
            {loading ? 'Ingresando…' : 'Iniciar Sesión'}
          </button>
        </form>
      </div>
    </main>
  );
}
