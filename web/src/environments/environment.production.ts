// Swapped in by the production build (angular.json fileReplacements).
// The API serves this build from its own wwwroot (ADR-016, docs/10), so a relative /api is the
// same origin: no CORS, and the SameSite=Strict refresh cookie is sent on every auth call.
export const environment = {
  production: true,
  apiUrl: '/api',
};
