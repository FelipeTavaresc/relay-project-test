# syntax=docker/dockerfile:1
#
# Build the Angular bundle, then serve it as static files. Two stages, no Node in the final image.

# Node 24 to match the version the lockfile was resolved with (npm 11): npm 10 in a node:22 image
# rejects this package-lock.json outright, reporting @emnapi/* entries as missing.
ARG NODE_IMAGE=node:24-alpine
ARG NGINX_IMAGE=nginx:alpine

# ---------------------------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------------------------
FROM ${NODE_IMAGE} AS build
WORKDIR /src

# `npm ci` from the committed lockfile — the same install the README documents, and cached until
# the lockfile changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# The SPA runs in the browser, so this is the origin the *user's* machine reaches the API on —
# the host port compose publishes for `api`, not the compose-internal `http://api:8080`.
# Baked in at build time because Angular compiles environment.ts into the bundle.
ARG API_BASE_URL=http://localhost:5141
ARG ACCOUNT_ID=6
# The two greps fail the build rather than shipping a silently unsubstituted default: if
# environment.ts is ever reshaped, this is where it gets noticed.
RUN set -eu; \
    sed -i "s#apiBaseUrl: '[^']*'#apiBaseUrl: '${API_BASE_URL}'#" src/environments/environment.ts; \
    sed -i "s#accountId: [0-9]\+#accountId: ${ACCOUNT_ID}#" src/environments/environment.ts; \
    grep -q "apiBaseUrl: '${API_BASE_URL}'" src/environments/environment.ts; \
    grep -q "accountId: ${ACCOUNT_ID}" src/environments/environment.ts

RUN npm run build

# ---------------------------------------------------------------------------------------------
# web
# ---------------------------------------------------------------------------------------------
FROM ${NGINX_IMAGE} AS web

COPY nginx.conf /etc/nginx/conf.d/default.conf
# `@angular/build:application` emits the browser bundle under dist/<project>/browser.
COPY --from=build /src/dist/frontend/browser /usr/share/nginx/html

# Left on the image's own user model — the nginx master process starts as root to bind :80 and
# drops its workers to the unprivileged `nginx` user. Running the master unprivileged as well
# would mean relocating the port, the pidfile and the cache dirs for a container that serves
# four static files; noted rather than done.
EXPOSE 80
