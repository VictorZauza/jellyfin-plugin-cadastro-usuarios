#!/usr/bin/env bash
# Gera o zip do plugin e o manifest.json com o checksum correto.
#
# Uso:
#   ./publicar.sh SEU-USUARIO-GITHUB [NOME-DO-REPO] [VERSAO] [BRANCH]
#
# Exemplo:
#   ./publicar.sh viquinho
#   ./publicar.sh viquinho jellyfin-cadastro-usuarios 1.1.0.0 main
#
# Depois é só: git add -A && git commit -m "v1.0.0.0" && git push

set -euo pipefail

USUARIO="${1:?Informe seu usuário do GitHub. Ex.: ./publicar.sh viquinho}"
REPO="${2:-jellyfin-plugin-cadastro-usuarios}"
VERSAO="${3:-1.0.0.0}"
BRANCH="${4:-main}"

RAIZ="$(cd "$(dirname "$0")" && pwd)"
PROJETO="$RAIZ/Jellyfin.Plugin.UserRegistration"
ZIP_NOME="cadastro-usuarios_${VERSAO}.zip"
BASE="https://raw.githubusercontent.com/${USUARIO}/${REPO}/${BRANCH}"

echo "==> Compilando a versão ${VERSAO}"
# mantém a versão do assembly igual à do manifest
cat > "$RAIZ/Directory.Build.props" <<EOF
<Project>
    <PropertyGroup>
        <Version>${VERSAO}</Version>
        <AssemblyVersion>${VERSAO}</AssemblyVersion>
        <FileVersion>${VERSAO}</FileVersion>
    </PropertyGroup>
</Project>
EOF

dotnet build "$PROJETO" -c Release --nologo

echo "==> Gerando $ZIP_NOME (a DLL precisa ficar na raiz do zip)"
mkdir -p "$RAIZ/dist"
rm -f "$RAIZ/dist/$ZIP_NOME"
(cd "$PROJETO/bin/Release/net9.0" && zip -qj "$RAIZ/dist/$ZIP_NOME" Jellyfin.Plugin.UserRegistration.dll)

CHECKSUM="$(md5sum "$RAIZ/dist/$ZIP_NOME" | cut -d' ' -f1 | tr 'a-f' 'A-F')"
DATA="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

echo "==> Escrevendo manifest.json (mantendo as versões anteriores)"
python3 - "$VERSAO" "$CHECKSUM" "$DATA" "$BASE/dist/$ZIP_NOME" "$USUARIO" "$RAIZ/manifest.json" <<'PYEOF'
import json, os, sys
versao, checksum, data, url, usuario, caminho = sys.argv[1:7]
pacote = {
    "guid": "b4f0a5c2-9d71-4f3a-8f21-6c2e5a7d41b9",
    "name": "Cadastro de Usuários",
    "description": "Tela pública para o visitante criar uma conta e um painel onde o administrador confirma cada cadastro.",
    "overview": "Cadastro de usuários com aprovação do administrador",
    "owner": usuario,
    "category": "General",
    "versions": [],
}
antigas = []
if os.path.exists(caminho):
    try:
        atual = json.load(open(caminho, encoding="utf-8"))
        if atual and isinstance(atual, list):
            antigas = [v for v in atual[0].get("versions", []) if v.get("version") != versao]
    except Exception:
        antigas = []
pacote["versions"] = [{
    "version": versao,
    "changelog": f"Versão {versao}.",
    "targetAbi": "10.11.0.0",
    "sourceUrl": url,
    "checksum": checksum,
    "timestamp": data,
}] + antigas
json.dump([pacote], open(caminho, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PYEOF

echo
echo "Pronto."
echo "  checksum: $CHECKSUM"
echo "  zip:      dist/$ZIP_NOME"
echo
echo "Agora:  git add -A && git commit -m \"v${VERSAO}\" && git push"
echo
echo "URL do repositório para colar no Jellyfin:"
echo "  ${BASE}/manifest.json"
