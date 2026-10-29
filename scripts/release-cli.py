"""CLI를 Release NativeAOT로 게시하고 독립 실행 배포 ZIP을 만든다."""

import argparse
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import tempfile
import zipfile


REPOSITORY = Path(__file__).resolve().parents[1]
SUPPORTED_RIDS = (
    "win-x64", "win-arm64",
    "linux-x64", "linux-arm64",
    "osx-x64", "osx-arm64",
)


def host_rid():
    operating_system = {
        "Windows": "win",
        "Linux": "linux",
        "Darwin": "osx",
    }.get(platform.system())
    architecture = {
        "amd64": "x64",
        "x86_64": "x64",
        "arm64": "arm64",
        "aarch64": "arm64",
    }.get(platform.machine().lower())
    if operating_system is None or architecture is None:
        raise ValueError("지원하는 빌드 호스트는 Windows/Linux/macOS x64·ARM64입니다.")
    return f"{operating_system}-{architecture}"


def project_version():
    result = subprocess.run(
        ["dotnet", "msbuild", "cli/cli.csproj", "-getProperty:Version", "-nologo"],
        cwd=REPOSITORY,
        check=True,
        capture_output=True,
        text=True,
    )
    version = result.stdout.strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?", version):
        raise ValueError(f"cli.csproj의 Version을 확인해 주세요: {version!r}")
    return version


def prepare_package(published, package, rid):
    binary_name = "cli.exe" if rid.startswith("win-") else "cli"
    shutil.copy2(published / binary_name, package / binary_name)
    if not rid.startswith("win-"):
        (package / binary_name).chmod(0o755)
    shutil.copyfile(REPOSITORY / "docs/cli-readme.md", package / "README.md")

    # GUI 설정이나 개인 인증 정보는 배포하지 않고 CLI 기본값만 포함한다.
    defaults = json.loads((REPOSITORY / "config/config.json").read_text(encoding="utf-8-sig"))
    config = {
        "Host": defaults["Host"],
        "BucketId": "",
        "Root": "",
        "WarningPatterns": defaults["WarningPatterns"],
    }
    config_directory = package / "config"
    config_directory.mkdir()
    (config_directory / "config.json").write_text(
        json.dumps(config, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def create_archive(package, destination):
    # 새 ZIP을 완성한 뒤 교체하여 실패 시 기존 배포물을 보존한다.
    with tempfile.TemporaryDirectory(prefix=".cli-zip-", dir=destination.parent) as temporary:
        archive_path = Path(temporary) / destination.name
        with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for path in sorted(package.rglob("*")):
                if path.is_file():
                    archive.write(path, path.relative_to(package).as_posix())
        os.replace(archive_path, destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", choices=SUPPORTED_RIDS, help="생략하면 현재 OS·아키텍처")
    arguments = parser.parse_args()
    current_rid = host_rid()
    rid = arguments.rid or current_rid
    if rid.split("-")[0] != current_rid.split("-")[0]:
        parser.error("NativeAOT는 대상 OS에서 빌드해야 합니다. 해당 OS에서 실행해 주세요.")

    version = project_version()
    staging = REPOSITORY / "artifacts/staging/cli" / rid
    releases = REPOSITORY / "artifacts/releases"
    staging.parent.mkdir(parents=True, exist_ok=True)
    releases.mkdir(parents=True, exist_ok=True)
    destination = releases / f"FishSyncClientCli-{rid}-{version}.zip"

    # 매번 빈 디렉토리에서 게시해 이전 결과물과 개인 파일이 섞이지 않게 한다.
    with tempfile.TemporaryDirectory(prefix=f".{rid}-", dir=staging.parent) as temporary:
        working_directory = Path(temporary)
        published = working_directory / "publish"
        package = working_directory / "package"
        package.mkdir()
        subprocess.run(
            [
                "dotnet", "publish", "cli/cli.csproj",
                "-c", "Release", "-r", rid,
                "--self-contained", "true",
                "-p:PublishAot=true", "-p:DebugType=None", "-p:DebugSymbols=false",
                "-o", str(published),
            ],
            cwd=REPOSITORY,
            check=True,
        )
        prepare_package(published, package, rid)
        create_archive(package, destination)
        if staging.exists():
            shutil.rmtree(staging)
        shutil.move(str(package), str(staging))

    print(f"배포 폴더: {staging}")
    print(f"배포 ZIP: {destination}")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        raise SystemExit(f"CLI 배포 실패: {error}") from error
