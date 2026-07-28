import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import { installDesignerApiFetch } from './auth/designerAuth'
import { initializeBrowserTelemetry } from '../../websites/shared/browserTelemetry.js'
import './styles/index.css'

installDesignerApiFetch()
initializeBrowserTelemetry({ application: 'designer' })

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </React.StrictMode>,
)
