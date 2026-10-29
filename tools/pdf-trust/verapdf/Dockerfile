FROM eclipse-temurin:11-jre-alpine@sha256:165a727edc500119b207bc01c81a7924e84c27549b6ef6debaed0f894489a2cb AS installer
WORKDIR /tmp/install
COPY install.xml ./install.xml
RUN wget -O installer.zip https://software.verapdf.org/develop/1.31/verapdf-greenfield-1.31.166-installer.zip \
    && printf '%s  %s\n' 25cc4d87daf1c6eb9efd40487141a03f23bb65a92ba59e49e0f33a01343038da installer.zip | sha256sum -c - \
    && unzip -q installer.zip \
    && java -jar verapdf-greenfield-1.31.166/verapdf-izpack-installer-1.31.166.jar install.xml

FROM eclipse-temurin:11-jre-alpine@sha256:165a727edc500119b207bc01c81a7924e84c27549b6ef6debaed0f894489a2cb
RUN addgroup -S verapdf && adduser -S -G verapdf -h /opt/verapdf verapdf
COPY --from=installer /opt/verapdf /opt/verapdf
USER verapdf
WORKDIR /data
ENTRYPOINT ["/opt/verapdf/verapdf"]
