import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ApiError } from './api/problem';
import { AuthProvider } from './auth/AuthContext';
import { RequireAuth } from './auth/RequireAuth';
import { P } from './auth/permissions';
import { Shell } from './components/Shell';
import { ToastProvider } from './components/Toast';
import { Categories, Clients, Suppliers } from './pages/Catalog';
import { Dashboard } from './pages/Dashboard';
import { Login } from './pages/Login';
import { Movements } from './pages/Movements';
import { NoAccess } from './pages/NoAccess';
import { NotFound } from './pages/NotFound';
import { ProductDetail } from './pages/ProductDetail';
import { Products } from './pages/Products';
import { Reports } from './pages/Reports';
import { RoleEditor } from './pages/RoleEditor';
import { Roles } from './pages/Roles';
import { Users } from './pages/Users';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Los datos de inventario cambian con cada movimiento, así que se
      // consideran frescos poco tiempo y se revalidan al volver a la pestaña.
      staleTime: 15_000,
      refetchOnWindowFocus: true,
      retry: (attempt, error) => {
        // No se reintenta lo que no va a cambiar reintentando: un 401 lo
        // resuelve el refresco del cliente, y un 4xx es una respuesta legítima.
        if (error instanceof ApiError && error.status < 500) return false;
        return attempt < 2;
      },
    },
    mutations: { retry: false },
  },
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <ToastProvider>
            <Routes>
              <Route path="/login" element={<Login />} />

              <Route element={<RequireAuth><Shell /></RequireAuth>}>
                {/* Cada ruta declara el mismo permiso que exige su endpoint. */}
                <Route
                  index
                  element={<RequireAuth permission={P.dashboard.view}><Dashboard /></RequireAuth>}
                />
                <Route
                  path="productos"
                  element={<RequireAuth permission={P.products.view}><Products /></RequireAuth>}
                />
                <Route
                  path="productos/:id"
                  element={<RequireAuth permission={P.products.view}><ProductDetail /></RequireAuth>}
                />
                <Route
                  path="movimientos"
                  element={<RequireAuth permission={P.movements.view}><Movements /></RequireAuth>}
                />
                <Route
                  path="reportes"
                  element={<RequireAuth permission={P.reports.view}><Reports /></RequireAuth>}
                />
                <Route
                  path="categorias"
                  element={<RequireAuth permission={P.categories.view}><Categories /></RequireAuth>}
                />
                <Route
                  path="proveedores"
                  element={<RequireAuth permission={P.suppliers.view}><Suppliers /></RequireAuth>}
                />
                <Route
                  path="clientes"
                  element={<RequireAuth permission={P.clients.view}><Clients /></RequireAuth>}
                />
                <Route
                  path="usuarios"
                  element={<RequireAuth permission={P.users.view}><Users /></RequireAuth>}
                />
                <Route
                  path="roles"
                  element={<RequireAuth permission={P.roles.view}><Roles /></RequireAuth>}
                />
                <Route
                  path="roles/:id"
                  element={<RequireAuth permission={P.roles.view}><RoleEditor /></RequireAuth>}
                />

                <Route path="sin-acceso" element={<NoAccess />} />
                <Route path="*" element={<NotFound />} />
              </Route>

              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </ToastProvider>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
