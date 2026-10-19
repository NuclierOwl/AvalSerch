# AvalNeroSercher — Avalonia + Emgu.CV + ML API
# Базовый образ Ubuntu 24.04 (noble) нужен для Emgu.CV.runtime.ubuntu-24.04-x64

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY AvalNeroSercher/AvalNeroSercher.csproj AvalNeroSercher/
RUN dotnet restore AvalNeroSercher/AvalNeroSercher.csproj

COPY AvalNeroSercher/ AvalNeroSercher/
WORKDIR /src/AvalNeroSercher
RUN dotnet publish AvalNeroSercher.csproj \
    -c Release \
    -o /app/publish \
    -r linux-x64 \
    --self-contained true \
    /p:PublishSingleFile=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

RUN apt-get update && apt-get install -y --no-install-recommends \
    # Avalonia / X11
    libx11-6 \
    libx11-xcb1 \
    libxcb1 \
    libxcursor1 \
    libxdamage1 \
    libxext6 \
    libxfixes3 \
    libxi6 \
    libxrandr2 \
    libxrender1 \
    libxss1 \
    libxtst6 \
    libice6 \
    libsm6 \
    libgl1 \
    libdrm2 \
    libgbm1 \
    libfontconfig1 \
    libfreetype6 \
    fonts-dejavu-core \
    fonts-noto \
    xvfb \
    # System.Drawing / Emgu.CV
    libgdiplus \
    libgtk-3-0 \
    libgstreamer1.0-0 \
    libgstreamer-plugins-base1.0-0 \
    libavcodec60 \
    libavformat60 \
    libavutil58 \
    libswscale7 \
    libdc1394-25 \
    libv4l-0 \
    libgeotiff5 \
    libusb-1.0-0 \
    libgflags2.2 \
    libgoogle-glog0v6t64 \
    liblapack3 \
    libva2 \
    libglut3.12 \
    libopengl0 \
    ocl-icd-libopencl1 \
    procps \
    && fc-cache -fv \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ENV DISPLAY=:99 \
    AVALNERO_API_HOST=0.0.0.0 \
    AVALNERO_API_PORT=5050 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LANG=C.UTF-8

WORKDIR /app
COPY --from=build /app/publish .
COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh \
    && mkdir -p /app/Ml_Model

# ML-модели монтируются сюда: -v ./Ml_Model:/app/Ml_Model
VOLUME ["/app/Ml_Model"]

EXPOSE 5050

ENTRYPOINT ["/entrypoint.sh"]
CMD ["./AvalNeroSercher"]
