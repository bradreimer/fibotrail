import { useState } from 'react';

export const INSTALL_HINT_KEY = 'fibotrail.install-hint-shown';

interface Device {
  userAgent: string;
  maxTouchPoints: number;
  /** Safari on iOS sets this: true when the app runs from the home screen. */
  standalone?: boolean;
}

/** True in Safari on an iPhone or iPad when the app isn't installed to the home screen. */
export function canInstallToHomeScreen(device: Device): boolean {
  // An iPad reports itself as a Mac, so touch points tell them apart.
  const isAppleTouch =
    /iPhone|iPad|iPod/.test(device.userAgent) ||
    (/Macintosh/.test(device.userAgent) && device.maxTouchPoints > 1);
  // Other iOS browsers can't add to the home screen the same way, so the hint would mislead.
  const isSafari = !/CriOS|FxiOS|EdgiOS|OPiOS/.test(device.userAgent);
  return isAppleTouch && isSafari && device.standalone === false;
}

function shouldShow(): boolean {
  try {
    if (!canInstallToHomeScreen(navigator as Device)) return false;
    if (localStorage.getItem(INSTALL_HINT_KEY)) return false;
    // The hint shows once. It's recorded when shown, so the next visit stays quiet.
    localStorage.setItem(INSTALL_HINT_KEY, '1');
    return true;
  } catch {
    // Storage is blocked, so "once" can't be kept. Showing nothing is the safe choice.
    return false;
  }
}

/** Explains Add to Home Screen, once, per ADR-007. */
export function InstallHint() {
  const [visible, setVisible] = useState(shouldShow);
  if (!visible) return null;
  return (
    <aside className="install-hint" aria-label="Install Fibotrail">
      <p className="install-hint__text">
        To use Fibotrail as an app, tap Share, then <strong>Add to Home Screen</strong>.
      </p>
      <button type="button" className="install-hint__close" onClick={() => setVisible(false)}>
        OK
      </button>
    </aside>
  );
}
