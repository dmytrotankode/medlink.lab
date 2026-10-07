#!/usr/bin/env bash
# =============================================================================
# MedLink LIS Analyzer Connector — збірка .deb (dpkg-deb) з publish linux-x64|linux-arm64
#   ./build-deb.sh [linux-x64|linux-arm64] [версія]
# Результат: artifacts/labconnector/medlink-labconnector_<версія>_<arch>.deb
# Встановлення: sudo apt install ./medlink-labconnector_4.0.0_amd64.deb
#               sudo -u medlink /opt/medlink/labconnector/MedLink.LabConnector setup --server … --install-key … --data-dir /var/lib/medlink/labconnector
# Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
# =============================================================================
set -euo pipefail
RID="${1:-linux-x64}"
VERSION="${2:-4.0.0}"
case "$RID" in linux-x64) DEB_ARCH=amd64;; linux-arm64) DEB_ARCH=arm64;; *) echo "RID має бути linux-x64 або linux-arm64"; exit 2;; esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH="$ROOT/artifacts/labconnector/$RID"
[[ -x "$PUBLISH/MedLink.LabConnector" ]] || "$SCRIPT_DIR/publish.sh" "$RID"

PKG="medlink-labconnector"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
DEST="$WORK/$PKG"
mkdir -p "$DEST/DEBIAN" "$DEST/opt/medlink/labconnector" "$DEST/lib/systemd/system" "$DEST/var/lib/medlink/labconnector/logs" "$DEST/usr/share/doc/$PKG"

cp -r "$PUBLISH"/. "$DEST/opt/medlink/labconnector/"
rm -rf "$DEST/opt/medlink/labconnector/data"
cp "$SCRIPT_DIR/README_UA.md" "$DEST/usr/share/doc/$PKG/"
cp "$SCRIPT_DIR/license.txt" "$DEST/usr/share/doc/$PKG/copyright"
sed -e "s#@INSTALL_DIR@#/opt/medlink/labconnector#g" -e "s#@DATA_DIR@#/var/lib/medlink/labconnector#g" "$SCRIPT_DIR/medlink-labconnector.service" > "$DEST/lib/systemd/system/medlink-labconnector.service"
chmod 755 "$DEST/opt/medlink/labconnector/MedLink.LabConnector"

INSTALLED_SIZE=$(du -sk "$DEST" | cut -f1)
cat > "$DEST/DEBIAN/control" <<EOF
Package: $PKG
Version: $VERSION
Section: misc
Priority: optional
Architecture: $DEB_ARCH
Installed-Size: $INSTALLED_SIZE
Maintainer: MedLink LLC <support@medlink.ua>
Depends: libc6, libgcc-s1, libssl3 | libssl1.1, libicu72 | libicu71 | libicu70 | libicu67 | libicu66, zlib1g
Recommends: cups-client
Homepage: https://medlink.ua
Description: MedLink LIS Analyzer Connector
 Коннектор лабораторних аналізаторів MedLink LIS 4.0: ASTM E1381/E1394,
 HL7 v2 (MLLP), текстові протоколи (RAPIDPoint, DRI-CHEM, Integra, Cyan…),
 TCP/COM/файловий обмін, офлайн-буфер SQLite, локальна сторінка статусу
 (http://localhost:5088/) та агент друку ZPL-етикеток.
EOF

cat > "$DEST/DEBIAN/conffiles" <<EOF
/opt/medlink/labconnector/appsettings.json
EOF

cat > "$DEST/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
if ! id -u medlink >/dev/null 2>&1; then useradd -r -s /usr/sbin/nologin -d /var/lib/medlink medlink; fi
for g in dialout tty lp; do getent group "$g" >/dev/null && usermod -a -G "$g" medlink || true; done
mkdir -p /var/lib/medlink/labconnector/logs
chown -R medlink:medlink /var/lib/medlink /opt/medlink/labconnector
systemctl daemon-reload || true
systemctl enable medlink-labconnector >/dev/null 2>&1 || true
if [ -f /var/lib/medlink/labconnector/appsettings.local.json ]; then
  systemctl restart medlink-labconnector || true
else
  echo "MedLink LabConnector встановлено. Зареєструйте коннектор:"
  echo "  sudo -u medlink /opt/medlink/labconnector/MedLink.LabConnector setup --server https://lis.clinic.ua --install-key КЛЮЧ --data-dir /var/lib/medlink/labconnector"
  echo "  sudo systemctl start medlink-labconnector"
fi
exit 0
EOF

cat > "$DEST/DEBIAN/prerm" <<'EOF'
#!/bin/sh
set -e
systemctl stop medlink-labconnector 2>/dev/null || true
systemctl disable medlink-labconnector 2>/dev/null || true
exit 0
EOF

cat > "$DEST/DEBIAN/postrm" <<'EOF'
#!/bin/sh
set -e
systemctl daemon-reload || true
if [ "$1" = "purge" ]; then rm -rf /var/lib/medlink/labconnector; fi
exit 0
EOF
chmod 755 "$DEST/DEBIAN/postinst" "$DEST/DEBIAN/prerm" "$DEST/DEBIAN/postrm"

OUT="$ROOT/artifacts/labconnector/${PKG}_${VERSION}_${DEB_ARCH}.deb"
dpkg-deb --build --root-owner-group "$DEST" "$OUT"
echo "Пакет зібрано: $OUT"
