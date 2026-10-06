# Build the host SPA from the monorepo root.
FROM node:24-alpine AS build
WORKDIR /repo
COPY . .
RUN npm ci
RUN npm run build -w @scs/shared
RUN npm run build -w @scs/host

# Serve the built SPA.
FROM node:24-alpine
ENV NODE_ENV=production PORT=8080
WORKDIR /app
COPY --from=build /repo/apps/host/dist ./dist
COPY apps/host/scripts ./scripts
EXPOSE 8080
CMD ["node", "scripts/serve.mjs"]
