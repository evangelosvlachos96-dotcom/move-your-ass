// Swapped in by the production build (angular.json fileReplacements).
// Phase 4 decides the real value: a relative /api works when Static Web Apps proxies to the
// App Service (same origin, cookie-friendly); otherwise this becomes the API's absolute origin.
export const environment = {
  production: true,
  apiUrl: '/api',
};
