FROM mcr.microsoft.com/playwright:v1.64.1-noble
RUN apt-get update && apt-get install -y --no-install-recommends libnss3-tools \
    && rm -rf /var/lib/apt/lists/*
USER pwuser
ENV HOME=/home/pwuser
