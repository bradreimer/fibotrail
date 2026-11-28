import type { CSSProperties } from 'react';

/** The ring's radius in its 40 by 40 drawing, and so the length of its full circle. */
const RADIUS = 16;
const CIRCLE = 2 * Math.PI * RADIUS;

/** The words next to the ring, such as "3 of 5 done today". */
export function dailyLine(played: number, total: number): string {
  return total > 0 && played >= total
    ? 'All done for today ✨'
    : `${played} of ${total} done today`;
}

/**
 * Today's progress across the games: a ring that fills as games are played, and the same in words.
 * A lost game counts as played. The ring is hidden from assistive technology, because the words
 * say the same thing. See docs/specs/daily-set.md#the-progress-ring.
 */
export function DailyRing({ played, total }: { played: number; total: number }) {
  const share = total === 0 ? 0 : Math.min(1, played / total);
  return (
    <div className="daily-ring">
      <svg className="daily-ring__ring" viewBox="0 0 40 40" aria-hidden="true" focusable="false">
        <circle className="daily-ring__track" cx="20" cy="20" r={RADIUS} />
        <circle
          className="daily-ring__fill"
          cx="20"
          cy="20"
          r={RADIUS}
          style={
            {
              '--daily-ring-circle': CIRCLE,
              '--daily-ring-gap': CIRCLE * (1 - share),
            } as CSSProperties
          }
        />
      </svg>
      <p className="daily-ring__text">{dailyLine(played, total)}</p>
    </div>
  );
}
