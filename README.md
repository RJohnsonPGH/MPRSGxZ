# MPRSGxZ
A web interface and library for controlling the Monoprice 4 and 6 zone amplifiers

## Running with Docker

The web app (`MPRSGxZ.Web`) can be run in a container via Docker Compose:

```sh
docker compose up --build
```

The app is reachable at **http://localhost:8080** on the host. The container itself listens
on all interfaces (`ASPNETCORE_URLS=http://+:8080`), and Docker Compose publishes the port on
all host interfaces by default, so other machines on the network can also reach it (subject to
your OS firewall). To restrict access to the host only, change the compose port to
`"127.0.0.1:8080:8080"`.

### Persistence

The SQLite database and log files are persisted in named Docker volumes so they survive
container restarts/recreation:

- `amp-data` → mounted at `/data` (SQLite database, `AMP_DATA_DIR`)
- `amp-logs` → mounted at `/logs` (rolling log files, `AMP_LOG_DIR`)

These paths can be overridden with the `AMP_DATA_DIR`/`AMP_DB_PATH` and `AMP_LOG_DIR`/`AMP_LOG_PATH`
environment variables (an explicit `*_PATH` value takes precedence over the corresponding `*_DIR`).

### Running behind a reverse proxy

The container serves plain HTTP only (TLS is expected to be terminated by a reverse proxy, e.g.
nginx, Caddy, or Traefik). To keep the app's SignalR hub (`/amphub`) and URL generation working
correctly behind such a proxy:

- The app trusts `X-Forwarded-Proto`/`X-Forwarded-For` headers by default (`AMP_TRUST_PROXY_HEADERS=true`),
  so it can correctly report `https` to the browser even though Kestrel itself only ever sees `http`
  from the proxy. Set `AMP_TRUST_PROXY_HEADERS=false` if you expose the container's port directly
  without a reverse proxy in front of it.
- Your reverse proxy must forward the `Upgrade` and `Connection` headers so SignalR can negotiate a
  WebSocket connection; without this it silently falls back to long-polling, which still works but is
  less responsive. For nginx, this looks like:

  ```nginx
  location / {
      proxy_pass http://localhost:8080;
      proxy_http_version 1.1;
      proxy_set_header Upgrade $http_upgrade;
      proxy_set_header Connection "upgrade";
      proxy_set_header X-Forwarded-Proto $scheme;
      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
      proxy_set_header Host $host;
  }
  ```
