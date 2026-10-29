FROM maven:3.9.11-eclipse-temurin-17@sha256:e4a7ace3dc0d645ed97f8d9ad0b0d3f0b14fa8d150138f27f116d7105a639b82 AS build

WORKDIR /src
COPY pom.xml .
COPY src ./src
RUN mvn --batch-mode --no-transfer-progress package

FROM eclipse-temurin:17-jre@sha256:c6f2875c05ea10f16398bdc5f73405c384991506f2f1ead8bcc6582ae8adea79

RUN groupadd --system validator && useradd --system --gid validator --home-dir /validator validator
WORKDIR /validator
COPY --from=build /src/target/pvad-dss-validator-1.0.0.jar /validator/validator.jar
USER validator
ENTRYPOINT ["java", "-jar", "/validator/validator.jar"]
