#!/bin/bash
# 编译 HearthwatchArena 补丁版（含 PrizeOverrides 奖励系统）
#
# 特点：不污染宿主机 —— 用一次性 dotnet/sdk 容器，编完即删。
# 适用：算力机 71，正式服容器名为 hearthwatch。
#
# 用法：
#   ./tools/build-patched-arena.sh
#
# 可覆盖的环境变量：
#   REPO      仓库根目录           （默认：脚本所在目录的上一级）
#   WORK      构建工作目录         （默认：$REPO/.arena-build）
#   CONTAINER 提供游戏引用的容器名 （默认：hearthwatch）
#   IMG       dotnet SDK 镜像      （默认：mcr.microsoft.com/dotnet/sdk:9.0）
#
# 说明：
#   游戏引用 DLL（assembly_valheim 等）从运行中的正式服容器里导出，
#   因为它们受版权保护、不能进仓库。因此本脚本必须在能 docker 访问
#   该容器的机器上运行。
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="${REPO:-$(cd "$HERE/.." && pwd)}"
WORK="${WORK:-$REPO/.arena-build}"
CONTAINER="${CONTAINER:-hearthwatch}"
IMG="${IMG:-mcr.microsoft.com/dotnet/sdk:9.0}"

SRC="$REPO/plugin"
OUT="$WORK/out"
REFS="$WORK/refs"

mkdir -p "$OUT" "$REFS"

echo "═══ 0. 路径 ═══"
echo "  仓库:   $REPO"
echo "  工作区: $WORK"
echo "  容器:   $CONTAINER"
echo

echo "═══ 1. 从正式服容器导出游戏引用 DLL ═══"
for f in assembly_valheim.dll assembly_utils.dll UnityEngine.dll \
         UnityEngine.CoreModule.dll UnityEngine.PhysicsModule.dll netstandard.dll; do
    if docker cp "$CONTAINER:/data/server/valheim_server_Data/Managed/$f" "$REFS/$f" 2>/dev/null; then
        echo "  ok: $f"
    else
        echo "  FAIL: $f"
    fi
done
for f in BepInEx.dll 0Harmony.dll; do
    if docker cp "$CONTAINER:/data/server/BepInEx/core/$f" "$REFS/$f" 2>/dev/null; then
        echo "  ok: $f"
    else
        echo "  FAIL: $f"
    fi
done

echo
echo "═══ 2. 引用 DLL 清单 ═══"
ls -la "$REFS/"

echo
echo "═══ 3. 用 dotnet/sdk 容器编译 ═══"
# 注意：挂载到容器内的 /src 下，让 csproj 在仓库里原地编译会污染仓库（写 obj/），
# 所以先把源码复制到工作区再编译。
rm -rf "$WORK/src" && mkdir -p "$WORK/src"
cp -r "$SRC/HearthwatchArena" "$WORK/src/"

docker run --rm \
  -v "$WORK/src/HearthwatchArena:/src" \
  -v "$REFS:/refs:ro" \
  -v "$OUT:/out" \
  "$IMG" \
  bash -c '
    set -e
    echo "--- SDK 版本 ---"
    dotnet --version
    echo
    echo "--- 编译 HearthwatchArena ---"
    dotnet build /src/HearthwatchArena.csproj \
      -c Release \
      -p:ValheimRefs=/refs \
      -o /out \
      2>&1 | tail -30
    echo
    echo "--- 产物 ---"
    ls -la /out/
  '

echo
echo "═══ 4. 编译结果 ═══"
ls -la "$OUT/" 2>/dev/null || echo "  无产物"

echo
echo "═══ 5. 部署提示 ═══"
echo "  产物: $OUT/HearthwatchArena.dll"
echo "  正式服通过 compose 只读挂载该文件到 /opt/hearthwatch/plugin/。"
echo "  替换后需重启容器才会加载新 DLL（外部改文件不会热加载）。"
