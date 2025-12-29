#!/bin/bash

echo "========================================="
echo "🚀 HaberBenim - Tüm Servisleri Başlat"
echo "========================================="

# Kill existing processes
echo "⏹️  Mevcut servisleri durduruyor..."
lsof -ti:5078 | xargs kill -9 2>/dev/null || true
lsof -ti:4200 | xargs kill -9 2>/dev/null || true
lsof -ti:4201 | xargs kill -9 2>/dev/null || true
pkill -f ngrok 2>/dev/null || true
sleep 2

# Start ngrok
echo "📡 ngrok başlatılıyor..."
ngrok http 5078 --log=stdout > /tmp/ngrok.log 2>&1 &
sleep 5

# Get ngrok URL
NGROK_URL=$(curl -s http://localhost:4040/api/tunnels 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['tunnels'][0]['public_url'])" 2>/dev/null)

if [ -z "$NGROK_URL" ]; then
    echo "❌ ngrok başlatılamadı!"
    exit 1
fi

echo "✅ ngrok: $NGROK_URL"

# Update environment.ngrok.ts
cat > /Users/omerbatuhantuncer/Documents/GitHub/HaberBenim/apps/admin-web/src/environments/environment.ngrok.ts << EOF
export const environment = {
  production: false,
  apiBaseUrl: '$NGROK_URL'
};
EOF
echo "✅ environment.ngrok.ts güncellendi"

# Start API
echo "🔧 API başlatılıyor..."
cd /Users/omerbatuhantuncer/Documents/GitHub/HaberBenim/apps/api/HaberPlatform.Api
dotnet run > /tmp/api-output.log 2>&1 &
sleep 10

# Check API
if curl -s 'http://localhost:5078/api/v1/public/latest?pageSize=1' | grep -q "items"; then
    echo "✅ API çalışıyor"
else
    echo "❌ API başlatılamadı!"
    cat /tmp/api-output.log | tail -20
    exit 1
fi

# Update PUBLIC_ASSET_BASE_URL in database
TOKEN=$(curl -s -X POST http://localhost:5078/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@local","password":"Admin123!"}' | python3 -c "import sys,json; print(json.load(sys.stdin).get('accessToken',''))")

if [ -n "$TOKEN" ]; then
    curl -s -X PUT http://localhost:5078/api/v1/settings/batch \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"settings\": [{\"key\": \"PUBLIC_ASSET_BASE_URL\", \"value\": \"$NGROK_URL\"}]}" > /dev/null
    echo "✅ PUBLIC_ASSET_BASE_URL güncellendi: $NGROK_URL"
fi

# Start Angular Admin
echo "🖥️  Angular Admin başlatılıyor..."
cd /Users/omerbatuhantuncer/Documents/GitHub/HaberBenim/apps/admin-web
npx ng serve --configuration=ngrok --port 4200 > /tmp/angular-admin.log 2>&1 &
sleep 12

if curl -s http://localhost:4200 | grep -q "app-root"; then
    echo "✅ Angular Admin çalışıyor"
else
    echo "⏳ Angular Admin hala yükleniyor..."
fi

echo ""
echo "========================================="
echo "✅ TÜM SERVİSLER HAZIR!"
echo "========================================="
echo "📡 ngrok:    $NGROK_URL"
echo "🔧 API:      http://localhost:5078"
echo "🖥️  Admin:   http://localhost:4200"
echo ""
echo "📷 Instagram: http://localhost:4200/integrations/instagram"
echo "========================================="

