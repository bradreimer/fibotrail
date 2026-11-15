import '@fontsource-variable/fraunces/index.css';
import '@fontsource-variable/inter/index.css';
import './styles/tokens.css';
import './styles/base.css';
import './styles/shell.css';
import './styles/states.css';
import './styles/map.css';
import './styles/inbox.css';

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';

import { App } from './App';
import { registerServiceWorker } from './registerServiceWorker';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);

registerServiceWorker();
