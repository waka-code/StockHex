import { useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ApiError, NetworkError } from '../api/problem';
import { useAuth } from '../auth/useAuth';
import { Field, Input } from '../components/Field';
import { Icon } from '../components/Icon';
import { Button } from '../components/ui';
import { ThemeToggle } from '../components/ThemeToggle';

const MOVEMENT_HELP = [
  { name: 'Entrada', desc: 'suma al stock', icon: 'down', color: 'var(--in)' },
  { name: 'Salida', desc: 'resta; falla si no alcanza', icon: 'right', color: 'var(--out)' },
  { name: 'Ajuste', desc: 'fija el stock del conteo físico', icon: 'filter', color: 'var(--adj)' },
];

export function Login() {
  const { isAuthenticated, login } = useAuth();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<ApiError | NetworkError | null>(null);
  const [busy, setBusy] = useState(false);

  if (isAuthenticated) {
    const from = (location.state as { from?: Location } | null)?.from;
    return <Navigate to={from?.pathname ?? '/'} replace />;
  }

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password);
    } catch (caught) {
      setError(caught instanceof ApiError || caught instanceof NetworkError
        ? caught
        : new NetworkError(caught));
    } finally {
      setBusy(false);
    }
  };

  const rateLimited = error instanceof ApiError && error.isRateLimited;

  return (
    <div
      style={{
        minHeight: '100vh', background: 'var(--page)', padding: 24,
        display: 'flex', alignItems: 'center', justifyContent: 'center' }}
    >
      <div style={{ position: 'fixed', top: 20, right: 20 }}><ThemeToggle /></div>

      <div
        style={{
          width: '100%', maxWidth: 1040,
          background: 'var(--surf)', border: '1px solid var(--bord)',
          borderRadius: 12, boxShadow: 'var(--shadow-lg)', overflow: 'hidden',
          display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))' }}
      >
        <form
          onSubmit={submit}
          style={{ padding: '44px 40px', display: 'flex', flexDirection: 'column', gap: 24 }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span
              style={{
                width: 32, height: 32, borderRadius: 7, background: 'var(--acc)',
                color: 'var(--acc-ink)', display: 'flex',
                alignItems: 'center', justifyContent: 'center' }}
            >
              <Icon name="logo" size={18} strokeWidth={2} />
            </span>
            <span style={{ fontSize: 19, fontWeight: 600, letterSpacing: '-.03em' }}>StockHex</span>
          </div>

          <div>
            <h1 style={{ fontSize: 22, fontWeight: 600, letterSpacing: '-.035em', margin: 0 }}>
              Iniciar sesión
            </h1>
            <p style={{ fontSize: 13, color: 'var(--ink2)', margin: '5px 0 0' }}>
              Control de inventario y movimientos de bodega.
            </p>
          </div>

          {error ? (
            <div
              role="alert"
              style={{
                display: 'flex', alignItems: 'flex-start', gap: 9, padding: '11px 13px',
                background: rateLimited ? 'var(--warn-bg)' : 'var(--dang-bg)',
                border: `1px solid ${rateLimited ? 'var(--warn-bord)' : 'var(--dang-bord)'}`,
                borderRadius: 'var(--r)',
                color: rateLimited ? 'var(--warn)' : 'var(--dang)' }}
            >
              <span style={{ marginTop: 1 }}><Icon name="alert" size={16} /></span>
              <div style={{ fontSize: 12.5, lineHeight: 1.5 }}>
                {error instanceof NetworkError
                  ? 'No se pudo conectar con el servidor.'
                  : rateLimited
                    ? 'Demasiados intentos'
                    : 'Email o contraseña incorrectos.'}
                <div style={{ color: 'var(--ink2)', marginTop: 3, fontSize: 11.5 }}>
                  {error instanceof NetworkError
                    ? 'Revisa que la API esté corriendo y que la URL sea la correcta.'
                    : error.message}
                </div>
              </div>
            </div>
          ) : null}

          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <Field label="Email" width="100%">
              <Input
                type="email"
                name="email"
                value={email}
                onChange={setEmail}
                placeholder="tu@empresa.cl"
                autoFocus
                error={Boolean(error) && !rateLimited}
              />
            </Field>
            <Field label="Contraseña" width="100%">
              <Input
                type="password"
                name="password"
                value={password}
                onChange={setPassword}
                placeholder="••••••••"
                error={Boolean(error) && !rateLimited}
              />
            </Field>
          </div>

          <Button
            type="submit"
            kind="primary"
            full
            loading={busy}
            disabled={!email.trim() || !password}
            style={{ height: 40, fontSize: 14 }}
          >
            Entrar
          </Button>

          <p
            style={{
              fontSize: 11.5, color: 'var(--ink3)', textAlign: 'center',
              lineHeight: 1.6, margin: 0 }}
          >
            La sesión se renueva automáticamente.
            <br />
            Tras 14 días sin actividad habrá que volver a entrar.
          </p>
        </form>

        <div
          style={{
            background: 'var(--nav-bg)', padding: '44px 40px',
            display: 'flex', flexDirection: 'column', justifyContent: 'center', gap: 22 }}
        >
          <div
            style={{
              fontSize: 11, fontWeight: 500, color: 'var(--acc)',
              textTransform: 'uppercase', letterSpacing: '.1em' }}
          >
            El stock nunca se edita a mano
          </div>
          <p
            style={{
              fontSize: 18, fontWeight: 500, color: '#fff',
              lineHeight: 1.5, letterSpacing: '-.02em', margin: 0 }}
          >
            Cada cambio de existencias queda registrado como un movimiento, con su autor,
            su contraparte y el stock antes y después.
          </p>
          <div style={{ height: 1, background: 'var(--nav-bord)' }} />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 13 }}>
            {MOVEMENT_HELP.map((item) => (
              <div key={item.name} style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
                <span
                  style={{
                    width: 26, height: 26, borderRadius: 6, flexShrink: 0,
                    background: 'rgba(255,255,255,.05)',
                    border: '1px solid var(--nav-bord)', color: item.color,
                    display: 'flex', alignItems: 'center', justifyContent: 'center' }}
                >
                  <Icon name={item.icon} size={14} />
                </span>
                <div>
                  <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--nav-ink)' }}>
                    {item.name}
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--nav-ink2)', marginTop: 1 }}>
                    {item.desc}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
