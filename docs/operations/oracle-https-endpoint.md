# Oracle HTTPS/WSS public endpoint

Project ETHER is reachable externally at:

- `https://game.rotagov.com.br` — HTTP API (auth, characters, game token, health)
- `wss://game.rotagov.com.br/game` — GameServer realtime WebSocket

## DNS

| Record | Name | Value |
| --- | --- | --- |
| A | `game.rotagov.com.br` | `143.47.112.212` |

## Runtime topology

```
Internet :443/:80
   │
   ▼
NGINX (host, Ubuntu) ── TLS (Let's Encrypt, certbot)
   ├── location /game  → http://127.0.0.1:18081  (GameServer, WS upgrade)
   └── location /      → http://127.0.0.1:18090  (API)
                              │
                              ▼
                    Docker network project-ether_default
                    api / game-server / worker
                              │
                              ▼
                    PostgreSQL (127.0.0.1:5432)  Redis (127.0.0.1:6379)
```

Only **443/80** are public. PostgreSQL, Redis, the API and the GameServer are
published on **loopback only** (`127.0.0.1`), which is preserved.

## NGINX vhost

`/etc/nginx/sites-available/game.rotagov.com.br.conf` (symlinked into
`sites-enabled`). Certbot manages the TLS listener and the HTTP→HTTPS redirect.

**Important:** `tailscaled` binds port 443 dual-stack on this host, so NGINX
cannot bind the wildcard `0.0.0.0:443`. All HTTPS vhosts therefore bind the
interface address explicitly (`listen 10.0.0.242:443 ssl;`). If a new HTTPS vhost
is added, it must use the same address or NGINX will fail to start
(`bind() to 0.0.0.0:443 failed`).

```nginx
server {
    server_name game.rotagov.com.br;

    location /game {
        proxy_pass http://127.0.0.1:18081;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
        proxy_buffering off;
    }

    location / {
        proxy_pass http://127.0.0.1:18090;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 60s;
        proxy_connect_timeout 10s;
    }

    listen 10.0.0.242:443 ssl; # managed by Certbot
    ssl_certificate /etc/letsencrypt/live/game.rotagov.com.br/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/game.rotagov.com.br/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;
}
```

## TLS

Issued with `certbot --nginx -d game.rotagov.com.br --redirect`. Renewal is
automatic via `certbot.timer` (systemd). Do not modify the other vhosts'
certificates.

## Validation

```bash
curl -sI https://game.rotagov.com.br/health     # 200
curl -s  https://game.rotagov.com.br/ready      # {"status":"ready",...}
# WebSocket upgrade:
curl -sS --http1.1 -o /dev/null -D - \
  -H "Connection: Upgrade" -H "Upgrade: websocket" \
  -H "Sec-WebSocket-Version: 13" -H "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==" \
  https://game.rotagov.com.br/game                # HTTP/1.1 101 Switching Protocols
```

Public ports must remain `80` and `443` only; `5432`, `6379`, `18090`, `18081`
must not be reachable from the Internet.
