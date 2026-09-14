FROM python:3.14-slim@sha256:cad9a2c871761c413caa6fdd6441c783451e740a48aaeba60ae62a8b53525ef6

RUN apt-get update \
    && apt-get install --yes --no-install-recommends openssl qpdf \
    && rm -rf /var/lib/apt/lists/*

RUN pip install --no-cache-dir \
    pyHanko==0.37.0 \
    pyhanko-cli==0.5.0

RUN qpdf --version | grep --quiet '12.2.0' \
    && pyhanko --version | grep --quiet '0.37.0'

ENTRYPOINT []
