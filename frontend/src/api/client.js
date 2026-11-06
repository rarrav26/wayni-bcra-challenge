import axios from 'axios';

// Base URL of the backend API. Configurable per environment via VITE_API_URL
// (see .env / .env.example). Defaults to the local API port used by docker-compose.
const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:8080';

const apiClient = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
});

export default apiClient;
