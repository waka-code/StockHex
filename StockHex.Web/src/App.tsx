import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ApiError } from './api/problem';
import { AuthProvider } from './auth/AuthContext';
import { RequireAuth } from './auth/RequireAuth';
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
                <Route index element={<Dashboard />} />
                <Route path="productos" element={<Products />} />
                <Route path="productos/:id" element={<ProductDetail />} />
                <Route path="movimientos" element={<Movements />} />
                <Route path="reportes" element={<Reports />} />

                <Route
                  path="categorias"
                  element={<RequireAuth roles={['Admin', 'Manager']}><Categories /></RequireAuth>}
                />
                <Route
                  path="proveedores"
                  element={<RequireAuth roles={['Admin', 'Manager']}><Suppliers /></RequireAuth>}
                />
                <Route
                  path="clientes"
                  element={<RequireAuth roles={['Admin', 'Manager']}><Clients /></RequireAuth>}
                />
                <Route
                  path="usuarios"
                  element={<RequireAuth roles={['Admin']}><Users /></RequireAuth>}
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
