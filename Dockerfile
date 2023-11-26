###syntax11 = docker/dockerfile:1.3

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /src
ENV TZ=Asia/Shanghai
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

COPY . .

RUN sed -i s@/deb.debian.org/@/mirrors.ustc.edu.cn/@g /etc/apt/sources.list && \
sed -i s@/snapshot.debian.org/@/mirrors.ustc.edu.cn/@g /etc/apt/sources.list && \
sed -i s@/security.debian.org/@/mirrors.ustc.edu.cn/@g /etc/apt/sources.list && \
sed -i s/cn.archive.ubuntu.com/mirrors.ustc.edu.cn/g /etc/apt/sources.list && \
sed -i s/archive.ubuntu.com/mirrors.ustc.edu.cn/g /etc/apt/sources.list && \
sed -i s/security.ubuntu.com/mirrors.ustc.edu.cn/g /etc/apt/sources.list && \
apt-get update -y && \
apt-get install clang zlib1g-dev -y

RUN dotnet restore Cloud189Checkin/Cloud189Checkin.csproj -s https://nuget.cdn.azure.cn/v3/index.json
RUN dotnet publish Cloud189Checkin/Cloud189Checkin.csproj -c Release -o /app -nowarn:cs0168,cs0105

RUN find /app -name "*.pdb"  | xargs rm -f
RUN find /app -name "*.dbg"  | xargs rm -f
RUN rm -f /app/appsettings.Development.json

#移除 OSX Windows 下的库
RUN rm -rf /app/runtimes/osx* /app/runtimes/win* /app/runtimes/*x86 /app/runtimes/linux-armel /app/runtimes/unix

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS final
ARG TARGETARCH
RUN arch=$TARGETARCH \
    && if [ "$TARGETARCH" = "amd64" ]; then arch="x64"; fi \
    && echo $arch > /tmp/arch
WORKDIR /app
EXPOSE 80
ENV TZ=Asia/Shanghai
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

COPY --from=build /app .

#指定IPv4优先
RUN echo precedence ::ffff:0:0/96 100 >> /etc/gai.conf

ENTRYPOINT ["./Cloud189Checkin"]
