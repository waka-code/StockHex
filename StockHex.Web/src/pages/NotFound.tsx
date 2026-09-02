import { Link } from 'react-router-dom';
import { Card, EmptyState } from '../components/ui';
import { usePageMeta } from '../lib/hooks';

export function NotFound() {
  usePageMeta({ title: 'Página no encontrada' });

  return (
    <Card>
      <EmptyState
        icon="alert"
        title="Esta página no existe"
        detail="Puede que el enlace esté mal escrito o que la sección se haya movido."
        action={<Link to="/" style={{ fontSize: 12.5 }}>Volver al Dashboard</Link>}
      />
    </Card>
  );
}
