import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { initTheme } from './components/theme';
import './styles/base.css';

// Antes de montar React, para que no haya un destello del tema claro
// cuando el usuario tiene elegido el oscuro.
initTheme();

const container = document.getElementById('root');
if (!container) throw new Error('Falta el elemento #root en index.html.');

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
