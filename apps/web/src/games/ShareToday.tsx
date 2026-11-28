import { useState } from 'react';

import { shareResult, type ShareOutcome } from './share';

/**
 * The text of Share today: one summary line, then each played game's share text in Games screen
 * order, with a blank line between games. Every part holds no trip name, place, date, or photo.
 * See docs/specs/daily-set.md#share-today.
 */
export function shareTodayText(
  number: number,
  shares: readonly (string | null | undefined)[],
): string {
  const played = shares.filter((share): share is string => Boolean(share));
  return [`Fibotrail #${number} · ${played.length} of ${shares.length} games`, ...played].join(
    '\n\n',
  );
}

/** The Share today button. It isn't shown until a game played today has a share text. */
export function ShareToday({
  number,
  shares,
}: {
  number: number;
  shares: readonly (string | null | undefined)[];
}) {
  const [shared, setShared] = useState<ShareOutcome | null>(null);
  if (!shares.some(Boolean)) return null;
  const label =
    shared === 'copied'
      ? 'Copied'
      : shared === 'failed'
        ? "Couldn't share. Try again."
        : 'Share today';
  return (
    <button
      type="button"
      className="button share-today"
      onClick={() => void shareResult(shareTodayText(number, shares)).then(setShared)}
    >
      {label}
    </button>
  );
}
