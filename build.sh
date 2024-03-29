docker buildx create --use --name arm64 default || true
# sed -i 's?mcr.microsoft.com/dotnet?imetric-docker.pkg.coding.net/dotnet/core?g' Dockerfile
docker buildx build --platform linux/amd64 \
-t hetaoos/cloud189checkin:latest \
-f Dockerfile --push .

