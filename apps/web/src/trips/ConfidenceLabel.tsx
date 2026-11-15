import type { components } from '../api/schema';

type Confidence = components['schemas']['TripConfidence'];

const LEVELS: Record<Confidence, { bars: number; word: string }> = {
  low: { bars: 1, word: 'Low confidence' },
  medium: { bars: 2, word: 'Medium confidence' },
  high: { bars: 3, word: 'High confidence' },
};

/** How sure Fibotrail is that this is a trip. An icon and a word, so color never carries it alone. */
export function ConfidenceLabel({ confidence }: { confidence: Confidence }) {
  const level = LEVELS[confidence];
  return (
    <span className="confidence">
      <svg className="confidence__icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false">
        {[0, 1, 2].map((bar) => (
          <rect
            key={bar}
            className={bar < level.bars ? 'confidence__bar confidence__bar--on' : 'confidence__bar'}
            x={1 + bar * 5}
            y={10 - bar * 4}
            width="4"
            height={5 + bar * 4}
            rx="1"
          />
        ))}
      </svg>
      {level.word}
    </span>
  );
}
