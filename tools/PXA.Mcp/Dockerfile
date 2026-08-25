FROM node:22-alpine AS build
WORKDIR /workspace

COPY tools/PXA.Mcp/package.json tools/PXA.Mcp/package-lock.json ./tools/PXA.Mcp/
RUN cd tools/PXA.Mcp && npm ci

COPY VERSION ./VERSION
COPY llms-full.txt ./llms-full.txt
COPY docs ./docs
COPY pxa-designer/src/docs ./pxa-designer/src/docs
COPY pxa-designer/src/chart/types.ts ./pxa-designer/src/chart/types.ts
COPY pxa-designer/src/data/elementRegistry.ts ./pxa-designer/src/data/elementRegistry.ts
COPY pxa-designer/src/types.ts ./pxa-designer/src/types.ts
COPY tools/PXA.Mcp ./tools/PXA.Mcp
RUN cd tools/PXA.Mcp && npm run typecheck && npm test && npm run build

FROM node:22-alpine AS runtime
ENV NODE_ENV=production \
    PXA_MCP_HOST=0.0.0.0 \
    PXA_MCP_PORT=3000
WORKDIR /app
RUN addgroup -S pxa && adduser -S -G pxa -h /app pxa

COPY --from=build /workspace/tools/PXA.Mcp/package.json /workspace/tools/PXA.Mcp/package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts \
    && npm cache clean --force \
    && rm -rf /usr/local/lib/node_modules/npm /usr/local/bin/npm /usr/local/bin/npx
COPY --from=build --chown=pxa:pxa /workspace/tools/PXA.Mcp/dist ./dist

USER pxa
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -qO- http://127.0.0.1:3000/health/ready >/dev/null || exit 1
CMD ["node", "dist/pxa-mcp-server.js"]
