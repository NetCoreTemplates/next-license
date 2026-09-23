import { expect, it } from "vitest";
import { detectPlatform, downloadableBuilds, packageLabel, platformLabel } from "./software-downloads";
it("offers installable builds instead of updater metadata", () => {
  const assets = [
    "studio.exe",
    "studio.exe.blockmap",
    "latest.yml",
    "studio.AppImage",
    "studio.deb",
    "studio.dmg",
    "source.tar.gz",
  ].map((name) => ({ name }));
  expect(downloadableBuilds(assets).map((x) => x.name)).toEqual([
    "studio.exe",
    "studio.AppImage",
    "studio.deb",
    "studio.dmg",
    "source.tar.gz",
  ]);
  expect(platformLabel("studio.AppImage")).toBe("Linux");
  expect(platformLabel("studio.exe")).toBe("Windows");
  expect(platformLabel("studio.dmg")).toBe("macOS");
});
it("names each package and recognises the visitor's platform", () => {
  expect(packageLabel("studio.AppImage")).toBe("AppImage");
  expect(packageLabel("studio_1.0_amd64.deb")).toBe("Debian / Ubuntu");
  expect(packageLabel("studio-setup.exe")).toBe("Installer");
  expect(detectPlatform("Mozilla/5.0 (Windows NT 10.0; Win64; x64)")).toBe("Windows");
  expect(detectPlatform("Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5)")).toBe("macOS");
  expect(detectPlatform("Mozilla/5.0 (X11; Linux x86_64)")).toBe("Linux");
  expect(detectPlatform("Mozilla/5.0 (Linux; Android 14)")).toBeUndefined();
});
