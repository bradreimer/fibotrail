import { useRef, useState, type PointerEvent, type ReactNode, type Ref } from 'react';

import type { components } from '../api/schema';
import { ConfidenceLabel } from '../trips/ConfidenceLabel';
import { countOf, formatDateRange } from '../trips/dates';
import { RoutePreview } from '../trips/RoutePreview';

export type Suggestion = components['schemas']['Suggestion'];

/** How far a card must be dragged, in pixels, for the swipe to count. */
export const SWIPE_DISTANCE = 96;

/** The sentence of a suggestion, in the voice of the design and branding document. */
export function suggestionSentence(suggestion: Suggestion): string {
  const dates = formatDateRange(suggestion.startDate, suggestion.endDate);
  const where = suggestion.place ? `${suggestion.place}, ${dates}` : dates;
  return `We found a possible trip: ${where}. ${countOf(suggestion.photoCount, 'photo')}.`;
}

interface SuggestionCardProps {
  suggestion: Suggestion;
  /** The buttons of the card. */
  actions: ReactNode;
  /** A swipe right on a touch screen. The buttons do the same, so a swipe is never the only way. */
  onSwipeRight?: () => void;
  onSwipeLeft?: () => void;
  headingRef?: Ref<HTMLHeadingElement>;
}

/** One suggested trip: the sentence, the places, the confidence, a photo strip, and a route preview. */
export function SuggestionCard({
  suggestion,
  actions,
  onSwipeRight,
  onSwipeLeft,
  headingRef,
}: SuggestionCardProps) {
  const start = useRef<{ x: number; y: number } | null>(null);
  const [offset, setOffset] = useState(0);
  const swipeable = Boolean(onSwipeRight ?? onSwipeLeft);

  function onPointerDown(event: PointerEvent) {
    // A swipe is a touch gesture. A mouse drag selects text, as it does everywhere else.
    if (swipeable && event.pointerType === 'touch')
      start.current = { x: event.clientX, y: event.clientY };
  }

  function onPointerMove(event: PointerEvent) {
    if (!start.current) return;
    const dx = event.clientX - start.current.x;
    const dy = event.clientY - start.current.y;
    // A mostly vertical move is a scroll, and the card stays put.
    setOffset(Math.abs(dx) > Math.abs(dy) ? dx : 0);
  }

  function onPointerEnd() {
    if (!start.current) return;
    start.current = null;
    if (offset >= SWIPE_DISTANCE) onSwipeRight?.();
    else if (offset <= -SWIPE_DISTANCE) onSwipeLeft?.();
    setOffset(0);
  }

  return (
    <article
      className="suggestion"
      style={
        offset === 0 ? undefined : { transform: `translateX(${offset}px)`, transition: 'none' }
      }
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerEnd}
      onPointerCancel={onPointerEnd}
    >
      <h2 className="suggestion__title" tabIndex={-1} ref={headingRef}>
        {suggestionSentence(suggestion)}
      </h2>
      <div className="suggestion__facts">
        {suggestion.places.length > 0 && <span>{suggestion.places.join(' · ')}</span>}
        <ConfidenceLabel confidence={suggestion.confidence} />
      </div>
      <div className="suggestion__media">
        <ul className="photo-strip" aria-label="Photos from this trip">
          {suggestion.strip.map((photo) => (
            <li key={photo.id} className="photo-strip__item">
              <img
                className="photo-strip__photo"
                src={`/api/photos/${photo.id}/thumbnail`}
                alt={photo.alt}
                loading="lazy"
                draggable={false}
              />
            </li>
          ))}
        </ul>
        <figure className="suggestion__route">
          <RoutePreview route={suggestion.route} />
          <figcaption className="map-caption">Photo route</figcaption>
        </figure>
      </div>
      <div className="suggestion__actions">{actions}</div>
    </article>
  );
}
