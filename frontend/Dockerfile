# Build the project
FROM node:alpine AS build-env
WORKDIR /nzb-webdav
COPY ./ ./
RUN npm install
RUN npm run build
RUN npm prune --omit=dev

# Build the production image
FROM node:alpine
EXPOSE 3000
WORKDIR /nzb-webdav
ENV NODE_ENV=production
COPY --from=build-env /nzb-webdav/node_modules ./node_modules
COPY --from=build-env /nzb-webdav/package.json ./package.json
COPY --from=build-env /nzb-webdav/server.js ./server.js
COPY --from=build-env /nzb-webdav/build ./build
CMD ["npm", "run", "start"]