FROM node:26.13.0-bookworm-slim@sha256:add5c17c14752c54d2f3d519ec46a3815684d41c33f198bcb09fb998f7dcbf68 AS build
WORKDIR /source
COPY package*.json .npmrc ./
RUN npm ci
COPY . .
RUN npm run check:api && npm run build

FROM nginx:1.30.0-alpine@sha256:0272e4604ed93c1792f03695a033a6e8546840f86e0de20a884bb17d2c924883
COPY deploy/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /source/dist /usr/share/nginx/html
EXPOSE 8080
