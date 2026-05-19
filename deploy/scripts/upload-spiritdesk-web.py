#!/usr/bin/env python3
"""
将 artifacts/spiritdesk-web-publish 同步到站点目录。
默认宝塔：/www/wwwroot/spiritdesk；删除旧文件时保留 data/（SQLite）。
若服务器存在 systemd 单元 spiritdesk-web.service 则 systemctl restart；
否则只对 dotnet SpiritDesk.Web.dll 做一次优雅重启 / 兜底拉起。

密码勿写入仓库，仅放在环境变量里。

  SPIRITDEPLOY_PW               必填
  SPIRITDEPLOY_HOST             默认 116.62.19.40
  SPIRITDEPLOY_USER             默认 root
  SPIRITDEPLOY_PORT             默认 22
  SPIRITDEPLOY_REMOTE_WEB       默认 /opt/spiritdesk/web
  SPIRITDEPLOY_URLS             默认 http://127.0.0.1:8080

依赖：pip install paramiko
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko


def repo_root_from_script() -> Path:
    return Path(__file__).resolve().parent.parent.parent


def ensure_remote_dir(sftp: paramiko.SFTPClient, remote_path: str) -> None:
    parts = [p for p in remote_path.replace("//", "/").split("/") if p]
    cur = ""
    for p in parts:
        cur += "/" + p
        try:
            sftp.stat(cur)
        except OSError:
            sftp.mkdir(cur)


def main() -> int:
    password = os.environ.get("SPIRITDEPLOY_PW", "").strip()
    if not password:
        print("请设置环境变量 SPIRITDEPLOY_PW", file=sys.stderr)
        return 2

    host = os.environ.get("SPIRITDEPLOY_HOST", "116.62.19.40").strip()
    user_ssh = os.environ.get("SPIRITDEPLOY_USER", "root").strip()
    port = int(os.environ.get("SPIRITDEPLOY_PORT", "22") or "22")
    remote_web = (
        os.environ.get("SPIRITDEPLOY_REMOTE_WEB", "/opt/spiritdesk/web").strip().rstrip("/")
    )
    asp_urls = os.environ.get("SPIRITDEPLOY_URLS", "http://127.0.0.1:8080").strip()

    local = repo_root_from_script() / "artifacts" / "spiritdesk-web-publish"
    if not local.is_dir():
        print(
            f"本地发布目录不存在，请先运行 deploy/scripts/publish-web.ps1:\n  {local}",
            file=sys.stderr,
        )
        return 2

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    try:
        client.connect(
            host,
            port=port,
            username=user_ssh,
            password=password,
            timeout=45,
            allow_agent=False,
            look_for_keys=False,
        )
    except paramiko.AuthenticationException as e:
        print(f"SSH 认证失败（请核对密码是否允许密码登录）。{e}", file=sys.stderr)
        return 1

    try:
        prep = f"""set -eu
WEB={remote_web!r}
mkdir -p "$WEB"
find "$WEB" -mindepth 1 -maxdepth 1 ! -name data -exec rm -rf {{}} +
"""

        footer = rf"""
WEB={remote_web!r}
URLS={asp_urls!r}

chown_pick() {{
  if id www >/dev/null 2>&1; then echo www:www
  elif id www-data >/dev/null 2>&1; then echo www-data:www-data
  elif id nginx >/dev/null 2>&1; then echo nginx:nginx
  elif id apache >/dev/null 2>&1; then echo apache:apache
  else echo root:root
  fi
}}

chown -R "$(chown_pick)" "$WEB"
mkdir -p "$WEB/data"
chown -R "$(chown_pick)" "$WEB/data" 2>/dev/null || true

if systemctl cat spiritdesk-web.service >/dev/null 2>&1; then
  systemctl daemon-reload
  systemctl restart spiritdesk-web
  systemctl is-active spiritdesk-web || true
  systemctl status spiritdesk-web --no-pager -l || true
  exit 0
fi

PID="$(pgrep -f '[d]otnet SpiritDesk.Web.dll' | head -1 || true)"
EXE=""
if [ -n "${{PID:-}}" ]; then
  EXE="$(readlink /proc/$PID/exe 2>/dev/null || true)"
fi
if [ -n "${{PID:-}}" ]; then
  kill -TERM "$PID" 2>/dev/null || true
fi

for _ in $(seq 1 25); do
  pgrep -f '[d]otnet SpiritDesk.Web.dll' >/dev/null || break
  sleep 1
done

if pgrep -f '[d]otnet SpiritDesk.Web.dll' >/dev/null; then
  echo "[info] dotnet SpiritDesk.Web 已由其它守护进程拉起，跳过手动启动。"
  pgrep -af 'dotnet SpiritDesk.Web.dll' || true
  exit 0
fi

DOTNET_EXE="$EXE"
if [ -z "$DOTNET_EXE" ] || [ ! -x "$DOTNET_EXE" ]; then
  DOTNET_EXE="$(ls -1 /www/server/dotnet/*/dotnet 2>/dev/null | sort -V | tail -1 || true)"
fi
if [ -z "${{DOTNET_EXE:-}}" ] || [ ! -x "$DOTNET_EXE" ]; then
  echo "[error] 未找到 dotnet 可执行文件" >&2
  exit 1
fi

nohup sudo -u www env \
  ASPNETCORE_ENVIRONMENT=Production \
  "ASPNETCORE_URLS=$URLS" \
  bash -lc 'cd '"$WEB"' && exec '"$DOTNET_EXE"' SpiritDesk.Web.dll' \
  >> /www/wwwlogs/spiritdesk-dotnet-deploy.log 2>&1 &

sleep 2
if ! pgrep -f '[d]otnet SpiritDesk.Web.dll' >/dev/null; then
  echo "[error] 启动 dotnet SpiritDesk.Web 失败（请查看 /www/wwwlogs/spiritdesk-dotnet-deploy.log）" >&2
  exit 1
fi
echo "[info] dotnet 已启动:"
pgrep -af 'dotnet SpiritDesk.Web.dll' || true
"""

        _, stdout, _ = client.exec_command(prep)
        if stdout.channel.recv_exit_status() != 0:
            print("远端准备目录失败", file=sys.stderr)
            return 1

        transport = client.get_transport()
        if transport is None:
            print("未取得 SSH transport", file=sys.stderr)
            return 1

        sftp = paramiko.SFTPClient.from_transport(transport)
        if sftp is None:
            print("未能打开 SFTP", file=sys.stderr)
            return 1

        ensure_remote_dir(sftp, remote_web)

        for root, dirs, files in os.walk(local):
            rel = Path(root).relative_to(local)
            remote_dir = (
                remote_web + "/" + "/".join(rel.parts).replace("\\", "/")
                if rel.parts
                else remote_web
            )
            ensure_remote_dir(sftp, remote_dir)
            for d in dirs:
                rd = remote_dir.rstrip("/") + "/" + d.replace("\\", "/")
                ensure_remote_dir(sftp, rd)
            for fname in files:
                lp = Path(root) / fname
                rp = remote_dir.rstrip("/") + "/" + fname.replace("\\", "/")
                sftp.put(str(lp), rp)

        sftp.close()

        stdin, stdout, stderr = client.exec_command("bash -eu")
        stdin.write(footer.encode())
        stdin.channel.shutdown_write()
        out = stdout.read().decode("utf-8", errors="replace")
        err = stderr.read().decode("utf-8", errors="replace")
        code = stdout.channel.recv_exit_status()
        sys.stdout.write(out)
        sys.stderr.write(err)
        print("REMOTE_EXIT=", code)
        return 0 if code == 0 else 1
    finally:
        try:
            client.close()
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
