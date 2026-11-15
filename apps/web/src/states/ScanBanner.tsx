interface ScanBannerProps {
  /** Photos read so far. */
  done: number;
  /** Photos in the library, or null while Fibotrail is still counting. */
  total: number | null;
}

const number = new Intl.NumberFormat('en-US');

/** The words of the banner, such as "Scanning your library: 12,400 of 48,000 photos." */
export function scanMessage(done: number, total: number | null): string {
  return total === null
    ? `Scanning your library: ${number.format(done)} photos so far.`
    : `Scanning your library: ${number.format(done)} of ${number.format(total)} photos.`;
}

/** The scanning state: a slim banner under the header. The app stays usable. */
export function ScanBanner({ done, total }: ScanBannerProps) {
  return (
    <div className="scan-banner" role="status">
      <span>{scanMessage(done, total)}</span>
      <progress
        className="scan-banner__bar"
        aria-label="Scan progress"
        max={total ?? undefined}
        value={total === null ? undefined : Math.min(done, total)}
      />
    </div>
  );
}

interface ScanFailedBannerProps {
  /** Why the scan stopped, in plain words. */
  message: string;
  onRetry: () => void;
}

/** Shown in place of the progress when a scan stops. The scan resumes from where it was. */
export function ScanFailedBanner({ message, onRetry }: ScanFailedBannerProps) {
  return (
    <div className="scan-banner scan-banner--failed" role="alert">
      <span>{message}</span>
      <button type="button" className="scan-banner__retry" onClick={onRetry}>
        Try again
      </button>
    </div>
  );
}
