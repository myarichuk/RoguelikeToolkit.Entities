#!/usr/bin/env python3
"""Builds a binary Unity (UPM) package tarball from a package template + netstandard2.1 builds.

The template directory (upm/<package-name>/) holds everything that is not a build output: package.json,
README.md, Runtime/link.xml, the Editor linker hook. `package`:

  1. builds each --project for netstandard2.1 and copies <Assembly>.dll/.xml/.pdb into Runtime/;
  2. stamps the version into package.json (and into the --sync-dependency entries);
  3. copies the repo LICENSE (and CHANGELOG.md, if present) next to package.json;
  4. writes a .meta file for every asset - Unity ignores meta-less assets in immutable (registry/tarball)
     packages - with GUIDs derived from package name + path, so they are stable across releases;
  5. runs `npm pack`, producing <out>/<package-name>-<version>.tgz.

`plugins` zips the full netstandard2.1 dependency closure (our assemblies + every third-party DLL + one merged
link.xml) for consumers who drop DLLs into Assets/Plugins instead of using the registries.

Third-party assemblies are never bundled into the tarball: they come from UnityNuGet (org.nuget.*) as package.json
dependencies. Two packages bundling the same DLL (Jint, YamlDotNet) break a Unity project.
"""

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile

SEMVER = re.compile(r"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")
TFM = "netstandard2.1"


def run(cmd, cwd=None):
    print("+ " + " ".join(cmd), flush=True)
    subprocess.run(cmd, cwd=cwd, check=True)


def build(project, version, out_dir, closure):
    cmd = ["dotnet", "build", project, "-c", "Release", "-f", TFM, "-o", out_dir,
           "-p:Version=" + version, "-p:CI=true", "-nologo"]
    if closure:
        cmd.append("-p:CopyLocalLockFileAssemblies=true")
    run(cmd)


def guid_for(package_name, rel_path):
    return hashlib.md5((package_name + "/" + rel_path.replace(os.sep, "/")).encode("utf-8")).hexdigest()


def importer_for(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".dll":
        # Any platform, editor included; no exclusions. Same as Unity's default for a managed plugin.
        return ("PluginImporter:\n"
                "  externalObjects: {}\n"
                "  serializedVersion: 2\n"
                "  iconMap: {}\n"
                "  executionOrder: {}\n"
                "  defineConstraints: []\n"
                "  isPreloaded: 0\n"
                "  isOverridable: 0\n"
                "  isExplicitlyReferenced: 0\n"
                "  validateReferences: 1\n"
                "  platformData:\n"
                "  - first:\n"
                "      Any: \n"
                "    second:\n"
                "      enabled: 1\n"
                "      settings: {}\n"
                "  userData: \n"
                "  assetBundleName: \n"
                "  assetBundleVariant: \n")
    if ext == ".cs":
        return ("MonoImporter:\n"
                "  externalObjects: {}\n"
                "  serializedVersion: 2\n"
                "  defaultReferences: []\n"
                "  executionOrder: 0\n"
                "  icon: {instanceID: 0}\n"
                "  userData: \n"
                "  assetBundleName: \n"
                "  assetBundleVariant: \n")
    if ext == ".asmdef":
        return ("AssemblyDefinitionImporter:\n"
                "  externalObjects: {}\n"
                "  userData: \n"
                "  assetBundleName: \n"
                "  assetBundleVariant: \n")
    if ext in (".md", ".txt", ".xml", ".json"):
        return ("TextScriptImporter:\n"
                "  externalObjects: {}\n"
                "  userData: \n"
                "  assetBundleName: \n"
                "  assetBundleVariant: \n")
    return ("DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


def write_metas(package_name, root):
    for dir_path, dir_names, file_names in os.walk(root):
        dir_names.sort()
        for name in sorted(dir_names):
            rel = os.path.relpath(os.path.join(dir_path, name), root)
            meta = os.path.join(dir_path, name + ".meta")
            if not os.path.exists(meta):
                with open(meta, "w", newline="\n") as f:
                    f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n" % guid_for(package_name, rel))
                    f.write(importer_for(name + "/"))
        for name in sorted(file_names):
            if name.endswith(".meta"):
                continue
            rel = os.path.relpath(os.path.join(dir_path, name), root)
            meta = os.path.join(dir_path, name + ".meta")
            if not os.path.exists(meta):
                with open(meta, "w", newline="\n") as f:
                    f.write("fileFormatVersion: 2\nguid: %s\n" % guid_for(package_name, rel))
                    f.write(importer_for(name))


def merge_link_xml(paths):
    """Concatenates the <assembly> entries of several link.xml files (Unity reads only files named link.xml)."""
    entries = []
    for path in paths:
        with open(path, encoding="utf-8") as f:
            body = f.read()
        entries.extend(m.group(0) for m in re.finditer(r"<assembly\b[^>]*?(/>|>.*?</assembly>)", body, re.S))
    return "<linker>\n" + "".join("  " + e + "\n" for e in dict.fromkeys(entries)) + "</linker>\n"


def pack(args):
    if not SEMVER.match(args.version):
        sys.exit("not a semver version (UPM rejects anything else): " + args.version)

    repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    template = os.path.abspath(args.package)
    out = os.path.abspath(args.out)
    os.makedirs(out, exist_ok=True)

    with open(os.path.join(template, "package.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    name = manifest["name"]
    manifest["version"] = args.version
    for dep in args.sync_dependency:
        if dep not in manifest.get("dependencies", {}):
            sys.exit("--sync-dependency %s is not a dependency in package.json" % dep)
        manifest["dependencies"][dep] = args.version

    work = tempfile.mkdtemp(prefix="upm-")
    try:
        staging = os.path.join(work, name)
        shutil.copytree(template, staging, ignore=shutil.ignore_patterns("*.meta"))
        with open(os.path.join(staging, "package.json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(manifest, f, indent=2)
            f.write("\n")

        runtime = os.path.join(staging, "Runtime")
        os.makedirs(runtime, exist_ok=True)
        for project in args.project:
            assembly = os.path.splitext(os.path.basename(project))[0]
            build_dir = os.path.join(work, "build", assembly)
            build(project, args.version, build_dir, closure=False)
            for ext in (".dll", ".xml", ".pdb"):
                src = os.path.join(build_dir, assembly + ext)
                if os.path.exists(src):
                    shutil.copy2(src, runtime)
                elif ext == ".dll":
                    sys.exit("build produced no " + src)

        shutil.copy2(os.path.join(repo_root, "LICENSE"), os.path.join(staging, "LICENSE.md"))
        changelog = os.path.join(repo_root, "CHANGELOG.md")
        if os.path.exists(changelog):
            shutil.copy2(changelog, staging)

        write_metas(name, staging)
        run(["npm", "pack", "--pack-destination", out], cwd=staging)
    finally:
        shutil.rmtree(work, ignore_errors=True)


def plugins(args):
    out = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    work = tempfile.mkdtemp(prefix="upm-plugins-")
    try:
        for project in args.project:
            build(project, args.version, work, closure=True)
        own = {os.path.splitext(os.path.basename(p))[0] for p in args.project}
        with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
            for file_name in sorted(os.listdir(work)):
                stem, ext = os.path.splitext(file_name)
                if ext == ".dll" or (stem in own and ext == ".xml"):
                    z.write(os.path.join(work, file_name), file_name)
            z.writestr("link.xml", merge_link_xml(args.link_xml))
    finally:
        shutil.rmtree(work, ignore_errors=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)

    p = commands.add_parser("package", help="build the UPM tarball for one package template")
    p.add_argument("--package", required=True, help="template dir, e.g. upm/com.rogueliketoolkit.entities")
    p.add_argument("--version", required=True, help="semver, e.g. 5.1.0")
    p.add_argument("--project", action="append", required=True, help="csproj whose assembly ships in Runtime/")
    p.add_argument("--sync-dependency", action="append", default=[],
                   help="package.json dependency pinned to --version (sibling package from the same repo)")
    p.add_argument("--out", required=True, help="output dir for <name>-<version>.tgz")
    p.set_defaults(func=pack)

    z = commands.add_parser("plugins", help="zip the full netstandard2.1 DLL closure (no-registry install)")
    z.add_argument("--version", required=True)
    z.add_argument("--project", action="append", required=True)
    z.add_argument("--link-xml", action="append", required=True, help="merged into the zip's single link.xml")
    z.add_argument("--out", required=True, help="zip path")
    z.set_defaults(func=plugins)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
