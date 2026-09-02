import { Link } from 'react-router-dom';
import { useCurrentUser } from '../auth/useAuth';
import { Card, EmptyState } from '../components/ui';
import { usePageMeta } from '../lib/hooks';

export function NoAccess() {
  const user = useCurrentUser();
  usePageMeta({ title: 'Sin acceso' });

  return (
    <Card>
      <EmptyState
        icon="lock"
        title="Esta sección no está disponible para tu rol"
        detail={`Tu cuenta tiene el rol ${user.role}. Si necesitas acceso, pídelo a un administrador.`}
        action={<Link to="/" style={{ fontSize: 12.5 }}>Volver al Dashboard</Link>}
      />
    </Card>
  );
}
