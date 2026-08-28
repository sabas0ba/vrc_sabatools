# Host の `nix develop` と同じ project 固有環境をコンテナ内に構築する。
# 構成は sabas0ba/dotfiles の Dockerfile と同じく、flake を profile として
# image build 時に実体化し、実行時には再評価しない。

ARG NIX_VERSION=2.35.1
ARG NIX_IMAGE_DIGEST=sha256:377d4887aca98f0dfa12971c1ea6d6a625a435d8b610d4c95a436843da6fbfd1
FROM nixos/nix:${NIX_VERSION}@${NIX_IMAGE_DIGEST}

RUN mkdir -p /etc/nix \
  && printf '%s\n' \
  'experimental-features = nix-command flakes' \
  'sandbox = false' \
  'filter-syscalls = false' \
  'max-jobs = auto' \
  'flake-registry = ' \
  >> /etc/nix/nix.conf

ENV SABATOOLS_PROFILE=/nix/var/nix/profiles/sabatools-dev
ENV DOTNET_CLI_HOME=/workspace/.verify/dotnet-home
ENV NUGET_PACKAGES=/workspace/.verify/nuget/packages

WORKDIR /workspace

# Toolchain definition is copied before the source tree so source/test changes
# do not invalidate the Nix closure layer.
COPY flake.nix flake.lock ./

RUN nix develop --profile "$SABATOOLS_PROFILE" --command true \
  && rm -rf /root/.cache/nix

COPY . .

COPY .github/verify/container-entrypoint.sh /usr/local/bin/sabatools-entrypoint.sh
RUN chmod +x /usr/local/bin/sabatools-entrypoint.sh

ENTRYPOINT ["/bin/sh", "/usr/local/bin/sabatools-entrypoint.sh"]
CMD []
