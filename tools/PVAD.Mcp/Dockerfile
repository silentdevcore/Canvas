FROM node:22-alpine AS build
WORKDIR /workspace

COPY tools/PVAD.Mcp/package.json tools/PVAD.Mcp/package-lock.json ./tools/PVAD.Mcp/
RUN cd tools/PVAD.Mcp && npm ci

COPY VERSION ./VERSION
COPY tools/configuration ./tools/configuration
COPY llms-full.txt ./llms-full.txt
COPY docs ./docs
COPY product-metadata/*.schema.json ./product-metadata/
COPY product-metadata/document-fidelity/*.schema.json ./product-metadata/document-fidelity/
COPY tools/pdfua/matterhorn-evidence.schema.json ./tools/pdfua/matterhorn-evidence.schema.json
COPY pvad-designer/src/docs ./pvad-designer/src/docs
COPY pvad-designer/src/chart/types.ts ./pvad-designer/src/chart/types.ts
COPY pvad-designer/src/data/elementRegistry.ts ./pvad-designer/src/data/elementRegistry.ts
COPY pvad-designer/src/types.ts ./pvad-designer/src/types.ts
COPY tools/PVAD.Mcp ./tools/PVAD.Mcp
RUN cd tools/PVAD.Mcp && npm run typecheck && npm test && npm run build

FROM node:22-alpine AS runtime
ENV NODE_ENV=production
WORKDIR /app
RUN addgroup -S pxa && adduser -S -G pxa -h /app pxa

COPY --from=build /workspace/tools/PVAD.Mcp/package.json /workspace/tools/PVAD.Mcp/package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts \
    && npm cache clean --force \
    && rm -rf /usr/local/lib/node_modules/npm /usr/local/bin/npm /usr/local/bin/npx
COPY --from=build --chown=pxa:pxa /workspace/tools/PVAD.Mcp/dist ./dist
COPY tools/PVAD.Mcp/scripts/container-entrypoint.sh /app/container-entrypoint.sh

USER pxa
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -qO- http://127.0.0.1:3000/health/ready >/dev/null || exit 1
ENTRYPOINT ["/bin/sh", "/app/container-entrypoint.sh"]
CMD ["node", "dist/pvad-mcp-server.js"]
