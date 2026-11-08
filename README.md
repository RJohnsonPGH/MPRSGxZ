# MPRSGxZ
A web app, CLI, and library for controlling the Monoprice 4 and 6 zone amplifiers. This app has not been tested, and likely **will not work**, with the 8 zone version of this amplifier, as it uses a different serial output.

If you have the 8 zone model of this amplifier, please open an issue. It should be simple to add support, but I do not have physical hardware to test against.

## Hardware

The app is designed to control the amplifier through an RS-232 serial server, specifically
the [USR-TCP232-302](https://www.amazon.com/USR-TCP232-302-Serial-Ethernet-Converter-Support/dp/B01GPGPEBM).
Other serial-to-ethernet servers may also work.


## Running with Docker

An image which runs the web app is published to GitHub Container Registry on each release: `ghcr.io/rjohnsonpgh/mprsgxz:latest` 

### Example docker-compose

The committed `docker-compose.yml` pulls the published image. A minimal working example:

```yaml
services:
  web:
    image: ghcr.io/rjohnsonpgh/mprsgxz:latest
    ports:
      - "8080:8080"
    environment:
      # - AMP_TRUST_PROXY_HEADERS=false  # uncomment if exposing the port directly
    volumes:
      - amp-data:/data
      - amp-logs:/logs
    restart: unless-stopped

volumes:
  amp-data:
  amp-logs:
```

Be sure to provide a host path or volume for `/data` if you want the configuration to persist across container reboots or updates.

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
  less responsive. 