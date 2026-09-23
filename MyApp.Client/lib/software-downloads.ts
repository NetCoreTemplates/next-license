import type { GitHubDownloadAsset } from "./dtos";
export function downloadableBuilds(assets: GitHubDownloadAsset[] = []) {
  return assets.filter((a) =>
    /\.(exe|msi|msix|dmg|pkg|appimage|deb|rpm|zip|tar\.gz|tar\.xz|tgz|apk)$/i.test(
      a.name ?? "",
    ),
  );
}
export function platformLabel(name = "") {
  if (/\.(exe|msi|msix)$/i.test(name)) return "Windows";
  if (/\.(dmg|pkg)$/i.test(name)) return "macOS";
  if (/\.(appimage|deb|rpm)$/i.test(name)) return "Linux";
  return "Download";
}
/** The package kind a person picks between when one platform has several builds. */
export function packageLabel(name = "") {
  const ext = /\.(tar\.gz|tar\.xz|[a-z0-9]+)$/i.exec(name)?.[1]?.toLowerCase();
  if (ext === "appimage") return "AppImage";
  if (ext === "deb") return "Debian / Ubuntu";
  if (ext === "rpm") return "Fedora / RHEL";
  if (ext === "exe" || ext === "msi" || ext === "msix" || ext === "pkg") return "Installer";
  if (ext === "dmg") return "Disk image";
  return ext ? `.${ext}` : "";
}
/** Best guess at the visitor's desktop platform, using the same labels as platformLabel. */
export function detectPlatform(userAgent = "") {
  if (/iphone|ipad|android/i.test(userAgent)) return undefined;
  if (/windows/i.test(userAgent)) return "Windows";
  if (/mac os|macintosh/i.test(userAgent)) return "macOS";
  if (/linux|x11|cros/i.test(userAgent)) return "Linux";
  return undefined;
}
