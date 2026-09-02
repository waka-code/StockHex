# ---------------------------------------------------------------- build
FROM node:22-alpine AS build
WORKDIR /app

# El manifiesto primero: la capa de dependencias queda en caché y no se repite
# en cada cambio de código.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Se compila SIN VITE_API_URL a propósito: la URL se resuelve al arrancar el
# contenedor, así una misma imagen sirve para cualquier despliegue.
RUN npm run build

# ---------------------------------------------------------------- runtime
FROM nginx:1.27-alpine AS final

COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY docker-entrypoint.d/ /docker-entrypoint.d/
COPY --from=build /app/dist /usr/share/nginx/html

# Los scripts de /docker-entrypoint.d los ejecuta la imagen de nginx al arrancar.
RUN chmod +x /docker-entrypoint.d/*.sh

EXPOSE 80

# 127.0.0.1 y no `localhost`: dentro del contenedor `localhost` resuelve primero
# a ::1 y nginx sólo escucha en IPv4, así que la sonda daba "connection refused"
# con el sitio funcionando perfectamente.
HEALTHCHECK --interval=15s --timeout=4s --start-period=8s --retries=4 \
  CMD wget -qO- http://127.0.0.1/healthz || exit 1
