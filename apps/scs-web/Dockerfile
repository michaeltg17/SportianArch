# Build the web component from the monorepo root.
FROM node:24-alpine AS build
WORKDIR /repo
COPY . .
RUN npm ci
RUN npm run build -w @scs/shared
RUN npm run build -w @scs/web

# Serve the built bundle (dist/) with CORS so any host origin can load it.
FROM node:24-alpine
ENV NODE_ENV=production PORT=5174
WORKDIR /app
COPY --from=build /repo/apps/scs-web/dist ./dist
COPY apps/scs-web/scripts ./scripts
EXPOSE 5174
CMD ["node", "scripts/serve.mjs"]
