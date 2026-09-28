export const environment = {
  production: false,
  // Same origin as the app, proxied to the API by the dev server (proxy.conf.json).
  //
  // Talking to http://localhost:5077 directly made development cross-origin while production is
  // same-origin, so the two behaved differently in exactly the place that matters: the refresh
  // cookie is SameSite=Strict, and WebKit would not send it across. Sessions silently died in
  // Safari locally and nowhere else. Proxying removes the difference, and removes the need for
  // CORS in development too.
  apiUrl: '/api',
};
